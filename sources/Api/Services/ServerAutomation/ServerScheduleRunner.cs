using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Records the due occurrences of the weekly schedule rules of a server exactly once.
/// </summary>
/// <remarks>
/// <para>
/// Every occurrence is processed in its own transaction under the server-row lock and is guarded by the unique run row
/// of its rule and instant. A start occurrence inserts its run row and queues the ordinary start job in the same
/// transaction; a stop occurrence commits a pending stop that the automation evaluator warns about (when the warning
/// window was open) or executes. A repeated pass, a crash between two steps or a second API instance therefore finds the
/// occurrence already recorded and never acts twice.
/// </para>
/// <para>
/// A start that comes due while a stop or terminate of the same server is still being processed does not start next to
/// it and is not skipped: its run row is stored as <see cref="EServerScheduleRunOutcome.WaitingForStop" />, the only
/// outcome a later pass replaces. Every pass re-collects the open waiting rows of the server, independent of the
/// watermark, and re-evaluates them under the server-row lock; once no stop is in flight any more the same row becomes
/// the started row in the transaction that queues the start job (or a skip). The waiting state lives only in the
/// PostgreSQL run row, so it is exactly once across repeated passes, a lost watermark commit, two instances and an API
/// restart, and nothing waits in memory.
/// </para>
/// <para>
/// Occurrences before the rule's effective instant or before the persisted watermark are never acted on, so saving a
/// rule never fires the past. The executor's reservation of the initial cost slice stays authoritative: a start that
/// is queued here and later fails shows as a failed job, and the rule's last run reads as not finished.
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context all reads and writes of a pass use.</param>
/// <param name="lifecycleService">The lifecycle service that queues the ordinary start job.</param>
/// <param name="costSliceService">The cost service that tells whether the cost owner can pay for a start.</param>
/// <param name="alertWriter">The writer that withdraws the alert of a superseded warning.</param>
/// <param name="auditWriter">The writer that appends the audit entries.</param>
/// <param name="options">The automation limits and timings; they are validated on construction.</param>
/// <param name="logger">The logger that receives sanitized diagnostics.</param>
[Scoped<ServerScheduleRunner, IServerScheduleRunner>]
internal sealed class ServerScheduleRunner(
    DatabaseContext dbContext,
    IGameServerLifecycleService lifecycleService,
    IHostingCostSliceService costSliceService,
    IAutoStopAlertWriter alertWriter,
    IHostingAuditWriter auditWriter,
    IOptions<ServerAutomationOptions> options,
    ILogger<ServerScheduleRunner> logger) : IServerScheduleRunner
{
    /// <summary>
    /// The longest span a scan looks back, so a rule that has never been scanned cannot flood the run table.
    /// </summary>
    private static readonly Duration MaxCatchUp = Duration.FromDays(8);

    /// <summary>
    /// The age of the watermark after which it is advanced even when no occurrence was due.
    /// </summary>
    private static readonly Duration WatermarkRefresh = Duration.FromMinutes(5);

    private readonly ServerAutomationOptions _options = Validated(options.Value);

    /// <inheritdoc />
    public async Task<ScheduleScanResult> ProcessDueAsync(
        Guid gameServerId,
        Instant now,
        CancellationToken cancellationToken)
    {
        var outcome = new ScanOutcome();
        var settings = await dbContext.ServerAutomationSettings
            .AsNoTracking()
            .Include(q => q.Schedules)
            .SingleOrDefaultAsync(q => q.GameServerFk == gameServerId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null || settings.Schedules.Count == 0)
            return outcome.ToResult();

        var zone = ScheduleCalculator.FindZone(settings.TimeZone);
        if (zone is null)
        {
            logger.LogWarning("Server {GameServerId} has schedule rules but no valid time zone.", gameServerId);
            return outcome.ToResult();
        }

        var watermark = await dbContext.ServerAutomationStates
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId)
            .Select(q => q.SchedulesEvaluatedThrough)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var due = CollectDue(settings.Schedules, zone, watermark ?? Instant.MinValue, now);
        var waiting = await LoadWaitingAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        foreach (var occurrence in MergeWaiting(due, waiting, settings.Schedules))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (occurrence.Waiting || occurrence.Rule.Action == EServerScheduleAction.Start)
            {
                if (await ProcessStartAsync(gameServerId, occurrence, zone, now, cancellationToken)
                        .ConfigureAwait(false))
                {
                    outcome.StartsQueued++;
                }
            }
            else
            {
                await ProcessStopAsync(gameServerId, occurrence, now, outcome, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Only newly collected occurrences drive the watermark, so a server with a waiting start causes no extra write.
        if (due.Count > 0 || watermark is null || now - watermark.Value >= WatermarkRefresh)
            await AdvanceWatermarkAsync(gameServerId, now, cancellationToken).ConfigureAwait(false);
        return outcome.ToResult();
    }

    /// <inheritdoc />
    public PendingStopCandidate? NextScheduledStop(
        ServerAutomationSettings settings,
        Instant now,
        Duration warningLead,
        IReadOnlySet<(Guid ScheduleId, Instant OccurrenceAt)>? handled = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var zone = ScheduleCalculator.FindZone(settings.TimeZone);
        if (zone is null)
            return null;

        PendingStopCandidate? best = null;
        foreach (var rule in settings.Schedules.Where(q => q.Action == EServerScheduleAction.Stop)
                     .OrderBy(q => q.Position))
        {
            // O - lead <= now < O  is the same as  now < O <= now + lead.
            foreach (var occurrence in ScheduleCalculator.OccurrencesBetween(
                         rule.DaysMask,
                         rule.MinuteOfDay,
                         zone,
                         now,
                         now + warningLead))
            {
                if (occurrence < rule.EffectiveFrom
                    || handled?.Contains((rule.PrimaryKey, occurrence)) == true
                    || best is not null && best.StopsAt <= occurrence)
                {
                    continue;
                }

                best = new PendingStopCandidate(
                    EAutoStopReason.Schedule,
                    null,
                    occurrence,
                    rule.PrimaryKey,
                    occurrence);
            }
        }

        return best;
    }

    private static ServerAutomationOptions Validated(ServerAutomationOptions automationOptions)
    {
        // The options are bound only in Program.cs, so every consumer validates them itself.
        automationOptions.Validate();
        return automationOptions;
    }

    /// <summary>
    /// Lists the occurrences that came due, in chronological order.
    /// </summary>
    /// <param name="rules">The rules of the server.</param>
    /// <param name="zone">The zone the rules are interpreted in.</param>
    /// <param name="watermark">The instant through which the rules were evaluated before.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <returns>The due occurrences, earliest first and by rule position for equal instants.</returns>
    private static List<DueOccurrence> CollectDue(
        IEnumerable<ServerSchedule> rules,
        DateTimeZone zone,
        Instant watermark,
        Instant now)
    {
        var due = new List<DueOccurrence>();
        foreach (var rule in rules)
        {
            var from = Instant.Max(Instant.Max(watermark, rule.EffectiveFrom), now - MaxCatchUp);
            due.AddRange(ScheduleCalculator
                .OccurrencesBetween(rule.DaysMask, rule.MinuteOfDay, zone, from, now)
                .Select(q => new DueOccurrence(rule, q, false)));
        }

        return due.OrderBy(q => q.Occurrence).ThenBy(q => q.Rule.Position).ToList();
    }

    /// <summary>
    /// Loads the open waiting run rows of a server, independent of the watermark.
    /// </summary>
    /// <param name="gameServerId">The server whose waiting starts are loaded.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The rule and instant of every start that waits for a stop of the server.</returns>
    private async Task<List<(Guid ScheduleId, Instant Occurrence)>> LoadWaitingAsync(
        Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ServerScheduleRuns
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId && q.Outcome == EServerScheduleRunOutcome.WaitingForStop)
            .Select(q => new { q.ScheduleFk, q.OccurrenceAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(q => (q.ScheduleFk, q.OccurrenceAt)).ToList();
    }

    /// <summary>
    /// Merges the open waiting starts into the newly collected occurrences without duplicating an occurrence.
    /// </summary>
    /// <param name="due">The newly collected occurrences.</param>
    /// <param name="waiting">The rule and instant of every open waiting start.</param>
    /// <param name="rules">The loaded rules of the server; a waiting start of a removed rule is dropped.</param>
    /// <returns>
    /// The occurrences to process, earliest first and by rule position for equal instants. A waiting start is always
    /// flagged so that it takes the start path, whatever the rule's current action.
    /// </returns>
    private static List<DueOccurrence> MergeWaiting(
        List<DueOccurrence> due,
        List<(Guid ScheduleId, Instant Occurrence)> waiting,
        IEnumerable<ServerSchedule> rules)
    {
        if (waiting.Count == 0)
            return due;

        var merged = due.ToList();
        var ruleMap = rules.ToDictionary(q => q.PrimaryKey);
        foreach (var (scheduleId, occurrence) in waiting)
        {
            if (!ruleMap.TryGetValue(scheduleId, out var rule))
                continue;

            var index = merged.FindIndex(q => q.Rule.PrimaryKey == scheduleId && q.Occurrence == occurrence);
            if (index >= 0)
                merged[index] = merged[index] with { Waiting = true };
            else
                merged.Add(new DueOccurrence(rule, occurrence, true));
        }

        return merged.OrderBy(q => q.Occurrence).ThenBy(q => q.Rule.Position).ToList();
    }

    /// <summary>
    /// Moves the persisted watermark forward to the evaluation instant, creating the state row when it is missing.
    /// </summary>
    /// <param name="gameServerId">The server whose watermark advances.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the transaction.</param>
    /// <returns>A task completing after the commit.</returns>
    private async Task AdvanceWatermarkAsync(Guid gameServerId, Instant now, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ServerRowLock.AcquireAsync(dbContext, gameServerId, cancellationToken).ConfigureAwait(false);
            var state = await dbContext.ServerAutomationStates
                .SingleOrDefaultAsync(q => q.GameServerFk == gameServerId, cancellationToken)
                .ConfigureAwait(false);
            if (state is null)
            {
                dbContext.ServerAutomationStates.Add(new ServerAutomationState
                {
                    PrimaryKey = Guid.NewGuid(),
                    GameServerFk = gameServerId,
                    SchedulesEvaluatedThrough = now,
                    TimeStampUpdated = now,
                });
            }
            else if (state.SchedulesEvaluatedThrough is not { } through || through < now)
            {
                state.SchedulesEvaluatedThrough = now;
                state.TimeStampUpdated = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Another instance created or advanced the row first; its value is as good as ours.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation(
                "Schedule watermark of server {GameServerId} was written concurrently: {Failure}",
                gameServerId,
                HostingFailureSanitizer.Sanitize(ex.Message));
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Processes one start occurrence: the missed, consent, stop, running, busy and cost checks, then either a queued
    /// start, a stored waiting state or a recorded skip.
    /// </summary>
    /// <param name="gameServerId">The server of the rule.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="zone">The zone the rule is interpreted in.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the transaction.</param>
    /// <returns><see langword="true" /> when a start job was queued.</returns>
    private async Task<bool> ProcessStartAsync(
        Guid gameServerId,
        DueOccurrence due,
        DateTimeZone zone,
        Instant now,
        CancellationToken cancellationToken)
    {
        string? busyReason;
        try
        {
            return await TryStartAsync(gameServerId, due, zone, now, cancellationToken).ConfigureAwait(false);
        }
        catch (HostingConflictException ex)
        {
            busyReason = HostingFailureSanitizer.Sanitize(ex.Message);
        }
        catch (HostingValidationException ex)
        {
            busyReason = HostingFailureSanitizer.Sanitize(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            // The unique run row exists: another pass or instance recorded this occurrence first.
            dbContext.ChangeTracker.Clear();
            logger.LogInformation(
                "Scheduled start of server {GameServerId} was recorded concurrently: {Failure}",
                gameServerId,
                HostingFailureSanitizer.Sanitize(ex.Message));
            return false;
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }

        await RecordBusyAsync(gameServerId, due, now, busyReason, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Runs the start transaction. Conflicts of the lifecycle service propagate after the transaction rolled back.
    /// </summary>
    /// <param name="gameServerId">The server of the rule.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="zone">The zone the rule is interpreted in.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the transaction.</param>
    /// <returns><see langword="true" /> when a start job was queued.</returns>
    /// <exception cref="HostingConflictException">The lifecycle service refused the start.</exception>
    /// <exception cref="HostingValidationException">The lifecycle service rejected the start request.</exception>
    private async Task<bool> TryStartAsync(
        Guid gameServerId,
        DueOccurrence due,
        DateTimeZone zone,
        Instant now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ServerRowLock.AcquireAsync(dbContext, gameServerId, cancellationToken).ConfigureAwait(false);
            var context = await LoadStartContextAsync(gameServerId, due, cancellationToken).ConfigureAwait(false);
            if (context is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var server = context.Server;
            var open = context.Open;
            var skip = await EvaluateStartSkipAsync(context, due.Occurrence, zone, now, cancellationToken)
                .ConfigureAwait(false);
            if (skip is { Outcome: EServerScheduleRunOutcome.WaitingForStop })
            {
                // The waiting row is the durable marker; an existing one is left untouched, so repeated passes write
                // nothing.
                if (open is null)
                {
                    ScheduledRunLedger.AddWaiting(dbContext, due.Rule.PrimaryKey, gameServerId, due.Occurrence, now);
                    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    logger.LogInformation(
                        "Scheduled start of server {GameServerId} waits for the stop of the server to finish.",
                        gameServerId);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (skip is { } skipped)
            {
                ScheduledRunLedger.RecordSkip(
                    dbContext,
                    auditWriter,
                    open,
                    due.Rule.PrimaryKey,
                    gameServerId,
                    server.CurrentCostOwnerFk,
                    due.Occurrence,
                    skipped.Outcome,
                    skipped.Reason,
                    now);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Scheduled start of server {GameServerId} skipped ({Outcome}): {Reason}",
                    gameServerId,
                    skipped.Outcome,
                    skipped.Reason);
                return false;
            }

            // The run row and the start job commit together, so the occurrence acts exactly once. A waiting row is
            // turned into the started row under the server-row lock of this transaction.
            ServerScheduleRun run;
            if (open is null)
            {
                run = ScheduledRunLedger.AddRun(
                    dbContext,
                    due.Rule.PrimaryKey,
                    gameServerId,
                    due.Occurrence,
                    EServerScheduleRunOutcome.Started,
                    null,
                    null,
                    now);
            }
            else
            {
                run = open;
                run.Outcome = EServerScheduleRunOutcome.Started;
                run.Details = null;
                run.RecordedAt = now;
            }

            var job = await lifecycleService
                .EnqueueAutomatedActionAsync(
                    new AutomatedLifecycleRequest(
                        gameServerId,
                        GameServerActions.Start,
                        ELifecycleJobTrigger.Schedule,
                        null,
                        now,
                        $"ScheduleId={due.Rule.PrimaryKey}; "
                        + $"Occurrence={due.Occurrence.ToString("g", CultureInfo.InvariantCulture)}"
                        + (open is null ? string.Empty : "; WaitedForStop=true")),
                    cancellationToken)
                .ConfigureAwait(false);
            run.LifecycleJobFk = job.PrimaryKey;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Records a start occurrence as busy in a fresh transaction after the lifecycle service refused it.
    /// </summary>
    /// <param name="gameServerId">The server of the rule.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="reason">The sanitized refusal message.</param>
    /// <param name="cancellationToken">A token that cancels the transaction.</param>
    /// <returns>A task completing after the commit.</returns>
    private async Task RecordBusyAsync(
        Guid gameServerId,
        DueOccurrence due,
        Instant now,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ServerRowLock.AcquireAsync(dbContext, gameServerId, cancellationToken).ConfigureAwait(false);
            var context = await LoadStartContextAsync(gameServerId, due, cancellationToken).ConfigureAwait(false);
            if (context is not null)
            {
                ScheduledRunLedger.RecordSkip(
                    dbContext,
                    auditWriter,
                    context.Open,
                    due.Rule.PrimaryKey,
                    gameServerId,
                    context.Server.CurrentCostOwnerFk,
                    due.Occurrence,
                    EServerScheduleRunOutcome.SkippedBusy,
                    $"the start could not be queued: {reason}",
                    now);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Scheduled start of server {GameServerId} skipped (busy): {Reason}",
                    gameServerId,
                    reason);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation(
                "Scheduled start of server {GameServerId} was recorded concurrently: {Failure}",
                gameServerId,
                HostingFailureSanitizer.Sanitize(ex.Message));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Decides what happens with a start occurrence that is not queued yet. The order is missed (first evaluation
    /// only), bound (open waiting row only), consent, stop in flight, stop failed, running, busy, cost.
    /// </summary>
    /// <param name="context">The tracked server, the current rule and the open waiting run row of the occurrence.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="zone">The zone the rule is interpreted in.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>
    /// The outcome and its reason: <see cref="EServerScheduleRunOutcome.WaitingForStop" /> while a stop of the server
    /// is in flight, a skip outcome that closes the occurrence, or <see langword="null" /> when the start may be
    /// queued. A failed stop is never escalated here; the occurrence is closed and the failure stays with a person.
    /// </returns>
    private async Task<(EServerScheduleRunOutcome Outcome, string Reason)?> EvaluateStartSkipAsync(
        StartContext context,
        Instant occurrence,
        DateTimeZone zone,
        Instant now,
        CancellationToken cancellationToken)
    {
        var server = context.Server;
        if (context.Open is null && now - occurrence > Duration.FromTimeSpan(_options.StartGraceWindow))
        {
            return (EServerScheduleRunOutcome.SkippedMissed,
                "the start came due while the platform was not running and its grace window has passed");
        }

        if (context.Open is not null)
        {
            // A waiting start ends here when its rule changed or the rule's next occurrence took over, so at most one
            // start per rule waits and a stuck stop can never release a backlog of starts.
            if (context.Rule.EffectiveFrom > occurrence)
            {
                return (EServerScheduleRunOutcome.SkippedStopUnfinished,
                    "the rule was changed while the start waited for the stop of the server");
            }

            if (ScheduleCalculator
                .OccurrencesBetween(context.Rule.DaysMask, context.Rule.MinuteOfDay, zone, occurrence, now)
                .Any())
            {
                return (EServerScheduleRunOutcome.SkippedStopUnfinished,
                    "the stop of the server had not finished when the rule's next start came due");
            }
        }

        var consent = await dbContext.ServerAutomationSettings
            .AsNoTracking()
            .Where(q => q.GameServerFk == server.PrimaryKey)
            .Select(q => new { q.StartConsentAt, q.StartConsentCostOwnerFk })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (consent is not { StartConsentAt: not null }
            || consent.StartConsentCostOwnerFk != server.CurrentCostOwnerFk)
        {
            return (EServerScheduleRunOutcome.SkippedConsent,
                "no start consent was given for the current cost owner");
        }

        if (await IsStopInFlightAsync(server.PrimaryKey, now, cancellationToken).ConfigureAwait(false))
            return (EServerScheduleRunOutcome.WaitingForStop, ScheduledRunLedger.WaitingDetails);

        if (await HasFailedStopAsync(server.PrimaryKey, cancellationToken).ConfigureAwait(false))
        {
            return (EServerScheduleRunOutcome.SkippedStopUnfinished,
                "the stop of the server failed and waits for a person");
        }

        if (await dbContext.GameServerDeployments
                .AsNoTracking()
                .AnyAsync(q => q.GameServerFk == server.PrimaryKey && q.TimeStampResolved == null, cancellationToken)
                .ConfigureAwait(false))
        {
            return (EServerScheduleRunOutcome.SkippedRunning, "the server already has an unresolved deployment");
        }

        if (await dbContext.LifecycleJobs
                .AsNoTracking()
                .AnyAsync(
                    q => q.GameServerFk == server.PrimaryKey
                         && (q.Status == ELifecycleJobStatus.Queued
                             || q.Status == ELifecycleJobStatus.Running
                             || q.Status == ELifecycleJobStatus.RetryPending
                             || q.Status == ELifecycleJobStatus.WaitingForWorker),
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return (EServerScheduleRunOutcome.SkippedBusy, "another lifecycle action is pending");
        }

        var owner = await dbContext.CostOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == server.CurrentCostOwnerFk, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null
            || owner.IsFrozen
            || !await costSliceService
                .CanReserveInitialSliceAsync(
                    owner.PrimaryKey,
                    server.EstimatedHourlyCostEur,
                    server.RuntimeProviderIdentifier,
                    now,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return (EServerScheduleRunOutcome.SkippedCost, "the cost owner cannot pay for the start");
        }

        return null;
    }

    /// <summary>
    /// Processes one stop occurrence: it either commits a pending stop for the evaluator to execute or records why the
    /// occurrence does not apply.
    /// </summary>
    /// <param name="gameServerId">The server of the rule.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="outcome">The collector of the alerts this scan withdrew.</param>
    /// <param name="cancellationToken">A token that cancels the transaction.</param>
    /// <returns>A task completing after the commit.</returns>
    private async Task ProcessStopAsync(
        Guid gameServerId,
        DueOccurrence due,
        Instant now,
        ScanOutcome outcome,
        CancellationToken cancellationToken)
    {
        var withdrawn = new List<ResolvedCostAlert>();
        await using var transaction = await BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ServerRowLock.AcquireAsync(dbContext, gameServerId, cancellationToken).ConfigureAwait(false);
            var server = await LoadServerIfOpenAsync(gameServerId, due, cancellationToken).ConfigureAwait(false);
            if (server is null
                || await dbContext.AutomationPendingStops
                    .AsNoTracking()
                    .AnyAsync(
                        q => q.ScheduleFk == due.Rule.PrimaryKey && q.ScheduleOccurrenceAt == due.Occurrence,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                // Nothing to do, or the evaluator already warned about this occurrence.
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var deployment = await dbContext.GameServerDeployments
                .AsNoTracking()
                .Where(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null)
                .OrderByDescending(q => q.TimeStampCreated)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            var skip = await ApplyStopAsync(server, deployment, due, now, withdrawn, cancellationToken)
                .ConfigureAwait(false);
            if (skip is { } skipped)
            {
                ScheduledRunLedger.AddSkip(
                    dbContext,
                    auditWriter,
                    due.Rule.PrimaryKey,
                    gameServerId,
                    deployment?.CostOwnerFk ?? server.CurrentCostOwnerFk,
                    due.Occurrence,
                    skipped.Outcome,
                    skipped.Reason,
                    now);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            outcome.Resolved.AddRange(withdrawn);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation(
                "Scheduled stop of server {GameServerId} was recorded concurrently: {Failure}",
                gameServerId,
                HostingFailureSanitizer.Sanitize(ex.Message));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Adds the pending stop of a due stop occurrence when it applies to the deployment.
    /// </summary>
    /// <param name="server">The tracked server.</param>
    /// <param name="deployment">The active deployment, or <see langword="null" />.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="withdrawn">The collector of the alerts of superseded warnings.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>The skip outcome and its reason, or <see langword="null" /> when a pending stop was added.</returns>
    private async Task<(EServerScheduleRunOutcome Outcome, string Reason)?> ApplyStopAsync(
        GameServer server,
        GameServerDeployment? deployment,
        DueOccurrence due,
        Instant now,
        List<ResolvedCostAlert> withdrawn,
        CancellationToken cancellationToken)
    {
        if (deployment is not { Status: EDeploymentStatus.Running })
            return (EServerScheduleRunOutcome.SkippedOff, "the server was not running");

        // A stop only applies to a deployment that was already running at the occurrence.
        if (deployment.TimeStampStarted is { } startedAt && startedAt > due.Occurrence)
            return (EServerScheduleRunOutcome.SkippedMissed, "the server was started after the scheduled stop time");

        if (await dbContext.LifecycleJobs
                .AsNoTracking()
                .AnyAsync(
                    q => q.DeploymentFk == deployment.PrimaryKey
                         && q.Kind == ELifecycleJobKind.Stop
                         && (q.Status == ELifecycleJobStatus.Queued
                             || q.Status == ELifecycleJobStatus.Running
                             || q.Status == ELifecycleJobStatus.RetryPending
                             || q.Status == ELifecycleJobStatus.WaitingForWorker
                             || q.Status == ELifecycleJobStatus.Failed),
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return (EServerScheduleRunOutcome.SkippedBusy, "a stop of the server is already queued");
        }

        var open = await dbContext.AutomationPendingStops
            .Where(q => q.DeploymentFk == deployment.PrimaryKey && q.Status == EAutoStopStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (open is not null)
        {
            // The earlier stop wins; one open warning per deployment.
            if (open.StopsAt <= due.Occurrence)
                return (EServerScheduleRunOutcome.SkippedBusy, "an earlier automatic stop already applies");

            if (await PendingStopWithdrawal
                    .ApplyAsync(
                        dbContext,
                        alertWriter,
                        auditWriter,
                        open,
                        EAutoStopStatus.Superseded,
                        PendingStopPlanner.EarlierStopReason,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false) is { } resolved)
            {
                withdrawn.Add(resolved);
            }

            // The superseded row must leave the unique pending index before the new one enters it.
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Committed without a warning so the occurrence cannot be lost; the evaluator executes it at once.
        dbContext.AutomationPendingStops.Add(new AutomationPendingStop
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            Reason = EAutoStopReason.Schedule,
            ScheduleFk = due.Rule.PrimaryKey,
            ScheduleOccurrenceAt = due.Occurrence,
            WarnedAt = null,
            StopsAt = due.Occurrence,
            Status = EAutoStopStatus.Pending,
            TimeStampCreated = now,
        });
        return null;
    }

    /// <summary>
    /// Tells whether a stop or terminate of the server is still being processed. That covers a job that is queued,
    /// running, retry-pending or waiting for its worker (whatever its deployment, so the short window between the
    /// resolved deployment and the finished job counts) and an automatic stop that is due and about to be executed on
    /// the unresolved deployment.
    /// </summary>
    /// <param name="gameServerId">The server to inspect.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns><see langword="true" /> when a stop of the server is in flight.</returns>
    private async Task<bool> IsStopInFlightAsync(Guid gameServerId, Instant now, CancellationToken cancellationToken)
    {
        if (await dbContext.LifecycleJobs
                .AsNoTracking()
                .AnyAsync(
                    q => q.GameServerFk == gameServerId
                         && (q.Kind == ELifecycleJobKind.Stop || q.Kind == ELifecycleJobKind.Terminate)
                         && (q.Status == ELifecycleJobStatus.Queued
                             || q.Status == ELifecycleJobStatus.Running
                             || q.Status == ELifecycleJobStatus.RetryPending
                             || q.Status == ELifecycleJobStatus.WaitingForWorker),
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        // A stop and a start that come due in one catch-up pass: the evaluator executes the pending stop right after.
        return await dbContext.AutomationPendingStops
            .AsNoTracking()
            .AnyAsync(
                q => q.GameServerFk == gameServerId
                     && q.Status == EAutoStopStatus.Pending
                     && q.StopsAt <= now
                     && dbContext.GameServerDeployments.Any(
                         d => d.PrimaryKey == q.DeploymentFk && d.TimeStampResolved == null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether the unresolved deployment of the server is held by a stop that failed: the latest stop or
    /// terminate job of that deployment, by creation instant and then sequence, has ended failed.
    /// </summary>
    /// <param name="gameServerId">The server to inspect.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns><see langword="true" /> when a failed stop keeps the deployment unresolved.</returns>
    private async Task<bool> HasFailedStopAsync(Guid gameServerId, CancellationToken cancellationToken)
    {
        var latest = await dbContext.LifecycleJobs
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId
                        && (q.Kind == ELifecycleJobKind.Stop || q.Kind == ELifecycleJobKind.Terminate)
                        && dbContext.GameServerDeployments.Any(
                            d => d.PrimaryKey == q.DeploymentFk && d.TimeStampResolved == null))
            .OrderByDescending(q => q.TimeStampCreated)
            .ThenByDescending(q => q.Sequence)
            .Select(q => (ELifecycleJobStatus?)q.Status)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return latest == ELifecycleJobStatus.Failed;
    }

    /// <summary>
    /// Loads the tracked server and the open waiting run row of a start occurrence while its rule still exists.
    /// </summary>
    /// <param name="gameServerId">The server of the rule.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>
    /// The server and the open row (<see langword="null" /> on the first evaluation), or <see langword="null" /> when
    /// the rule is gone or the occurrence already has a final run row.
    /// </returns>
    private async Task<StartContext?> LoadStartContextAsync(
        Guid gameServerId,
        DueOccurrence due,
        CancellationToken cancellationToken)
    {
        // The rule is read again inside the transaction so a change since the scan started is seen.
        var rule = await dbContext.ServerSchedules
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == due.Rule.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
            return null;

        var open = await ScheduledRunLedger.FindAsync(dbContext, due.Rule.PrimaryKey, due.Occurrence, cancellationToken)
            .ConfigureAwait(false);
        if (open is not null && open.Outcome != EServerScheduleRunOutcome.WaitingForStop)
            return null;

        var server = await dbContext.GameServers
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false);
        return server is null ? null : new StartContext(server, rule, open);
    }

    /// <summary>
    /// Loads the tracked server while its rule still exists and its occurrence has no run row.
    /// </summary>
    /// <param name="gameServerId">The server of the rule.</param>
    /// <param name="due">The due occurrence.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>The server, or <see langword="null" /> when the occurrence must not be processed.</returns>
    private async Task<GameServer?> LoadServerIfOpenAsync(
        Guid gameServerId,
        DueOccurrence due,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ServerSchedules
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == due.Rule.PrimaryKey, cancellationToken)
                .ConfigureAwait(false)
            || await ScheduledRunLedger.ExistsAsync(dbContext, due.Rule.PrimaryKey, due.Occurrence, cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        return await dbContext.GameServers
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginAsync(
        CancellationToken cancellationToken)
        => dbContext.Database.BeginTransactionAsync(
            ServerRowLock.IsPostgreSql(dbContext) ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
            cancellationToken);

    /// <summary>
    /// A rule occurrence that came due.
    /// </summary>
    /// <param name="Rule">The rule the occurrence belongs to.</param>
    /// <param name="Occurrence">The occurrence instant.</param>
    /// <param name="Waiting">
    /// <see langword="true" /> when the occurrence has an open <see cref="EServerScheduleRunOutcome.WaitingForStop" />
    /// run row; it then always takes the start path, whatever the rule's current action.
    /// </param>
    private sealed record DueOccurrence(ServerSchedule Rule, Instant Occurrence, bool Waiting);

    /// <summary>
    /// The tracked server of a start occurrence together with its open waiting run row.
    /// </summary>
    /// <param name="Server">The tracked server.</param>
    /// <param name="Rule">The current definition of the rule, read inside the transaction.</param>
    /// <param name="Open">
    /// The tracked <see cref="EServerScheduleRunOutcome.WaitingForStop" /> run row, or <see langword="null" /> when the
    /// occurrence is evaluated for the first time.
    /// </param>
    private sealed record StartContext(GameServer Server, ServerSchedule Rule, ServerScheduleRun? Open);

    /// <summary>
    /// Collects what the committed transactions of one scan raised, withdrew and queued.
    /// </summary>
    private sealed class ScanOutcome
    {
        /// <summary>
        /// Gets the warning alerts the scan recorded.
        /// </summary>
        /// <value>The recorded alerts.</value>
        public List<RaisedCostAlert> Raised { get; } = [];

        /// <summary>
        /// Gets the warning alerts the scan withdrew.
        /// </summary>
        /// <value>The withdrawn alerts.</value>
        public List<ResolvedCostAlert> Resolved { get; } = [];

        /// <summary>
        /// Gets or sets the number of start jobs the scan queued.
        /// </summary>
        /// <value>The queued start count.</value>
        public int StartsQueued { get; set; }

        /// <summary>
        /// Converts the collected facts into the result.
        /// </summary>
        /// <returns>The scan result.</returns>
        public ScheduleScanResult ToResult() => new(Raised, Resolved, StartsQueued);
    }
}
