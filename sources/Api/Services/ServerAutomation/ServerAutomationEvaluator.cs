using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The persistence shell around <see cref="PendingStopPlanner" />: it gathers the facts of one server inside a
/// transaction, applies the planner's decision, and records every warning, withdrawal and stop durably.
/// </summary>
/// <remarks>
/// <para>
/// Every server is evaluated in its own transaction under the server-row lock, so a failure never affects another
/// server and a second API instance waits instead of deciding twice. A warning is one pending stop, one alert with its
/// deliveries and one audit entry, committed together before any side effect (persist intent first).
/// </para>
/// <para>
/// A due warning is executed exactly once: a conditional claim from <see cref="EAutoStopStatus.Pending" /> to
/// <see cref="EAutoStopStatus.Executed" /> and the ordinary stop job share one transaction. When another lifecycle
/// action conflicts, both roll back and the next pass retries. Only stop jobs are ever queued; a failed stop leaves
/// the deployment not running, so no candidate arises again and nothing escalates to a termination (D-AUTO-2).
/// </para>
/// <para>
/// The evaluator reads no clock and holds no timer: the caller passes the instant, and all timing state lives in
/// PostgreSQL.
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context all reads and writes of a pass use.</param>
/// <param name="gameDefinitions">The catalog that tells which games report player counts.</param>
/// <param name="metricsService">The metrics service that supplies the current player count.</param>
/// <param name="lifecycleService">The lifecycle service that queues the ordinary stop job.</param>
/// <param name="scheduleRunner">The runner that records due schedule occurrences before each server is evaluated.</param>
/// <param name="alertWriter">The writer that records and withdraws the warning alert.</param>
/// <param name="auditWriter">The writer that appends the audit entries.</param>
/// <param name="options">The automation limits and timings; they are validated on construction.</param>
/// <param name="logger">The logger that receives sanitized diagnostics.</param>
[Scoped<ServerAutomationEvaluator, IServerAutomationEvaluator>]
internal sealed class ServerAutomationEvaluator(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IServerMetricsService metricsService,
    IGameServerLifecycleService lifecycleService,
    IServerScheduleRunner scheduleRunner,
    IAutoStopAlertWriter alertWriter,
    IHostingAuditWriter auditWriter,
    IOptions<ServerAutomationOptions> options,
    ILogger<ServerAutomationEvaluator> logger) : IServerAutomationEvaluator
{
    /// <summary>
    /// The audit note of a maximum runtime stop that came due while nobody evaluated.
    /// </summary>
    private const string DowntimeNote = "came due while the platform was not running; no warning could be sent";

    private readonly Duration _warningLead = ValidatedLead(options.Value);
    private readonly ServerAutomationOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServerAutomationEvaluationResult> EvaluateAsync(Instant now, CancellationToken cancellationToken)
    {
        var raised = new List<RaisedCostAlert>();
        var resolved = new List<ResolvedCostAlert>();
        var jobsQueued = 0;
        foreach (var serverId in await SelectServerIdsAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // Due schedule occurrences are recorded first so the reconcile below sees their pending stops. A
                // failure here must not keep the other automatic stops of the server from being evaluated.
                var scan = await ScanSchedulesAsync(serverId, now, cancellationToken).ConfigureAwait(false);
                raised.AddRange(scan.RaisedAlerts);
                resolved.AddRange(scan.ResolvedAlerts);
                jobsQueued += scan.StartsQueued;
                var outcome = await EvaluateServerAsync(serverId, now, cancellationToken).ConfigureAwait(false);
                raised.AddRange(outcome.Raised);
                resolved.AddRange(outcome.Resolved);
                jobsQueued += outcome.JobsQueued;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception ex)
            {
                dbContext.ChangeTracker.Clear();
                logger.LogError(
                    "Server automation failed for server {GameServerId} and will retry: {Failure}",
                    serverId,
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }
        }

        dbContext.ChangeTracker.Clear();
        return new ServerAutomationEvaluationResult(raised, resolved, jobsQueued);
    }

    /// <summary>
    /// Runs the schedule scan of one server and converts a failure into an empty result.
    /// </summary>
    /// <param name="serverId">The server to scan.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the scan.</param>
    /// <returns>The scan result, empty after a failure.</returns>
    private async Task<ScheduleScanResult> ScanSchedulesAsync(
        Guid serverId,
        Instant now,
        CancellationToken cancellationToken)
    {
        try
        {
            return await scheduleRunner.ProcessDueAsync(serverId, now, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            dbContext.ChangeTracker.Clear();
            logger.LogError(
                "Schedule scan failed for server {GameServerId} and will retry: {Failure}",
                serverId,
                HostingFailureSanitizer.Sanitize(ex.Message));
            return new ScheduleScanResult([], [], 0);
        }
    }

    private static Duration ValidatedLead(ServerAutomationOptions automationOptions)
    {
        // The options are bound only in Program.cs, so every consumer validates them itself.
        automationOptions.Validate();
        return Duration.FromTimeSpan(automationOptions.WarningLead);
    }

    /// <summary>
    /// Selects the servers that need evaluation: those with a limit switched on, those that still hold an open
    /// warning, those with schedule rules, and those with a running idle clock, so a disabled setting can withdraw its
    /// warning and reset its clock.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>The distinct server ids.</returns>
    private async Task<IReadOnlyList<Guid>> SelectServerIdsAsync(CancellationToken cancellationToken)
    {
        var configured = await dbContext.ServerAutomationSettings
            .AsNoTracking()
            .Where(q => (q.IdleShutdownEnabled || q.MaxRuntimeEnabled) && !q.GameServer!.IsArchived)
            .Select(q => q.GameServerFk)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var pending = await dbContext.AutomationPendingStops
            .AsNoTracking()
            .Where(q => q.Status == EAutoStopStatus.Pending)
            .Select(q => q.GameServerFk)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        // Servers with schedule rules need their occurrences recorded and their stop windows watched.
        var scheduled = await dbContext.ServerSchedules
            .AsNoTracking()
            .Where(q => !q.GameServer!.IsArchived)
            .Select(q => q.GameServerFk)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        // A running idle clock of a server whose idle shutdown was switched off must still be reset.
        var clocks = await dbContext.ServerAutomationStates
            .AsNoTracking()
            .Where(q => q.IdleDeploymentFk != null)
            .Select(q => q.GameServerFk)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return configured.Concat(pending).Concat(scheduled).Concat(clocks).Distinct().ToArray();
    }

    /// <summary>
    /// Evaluates one server inside its own transaction.
    /// </summary>
    /// <param name="serverId">The server to evaluate.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the transaction.</param>
    /// <returns>What the committed transaction raised, withdrew and queued.</returns>
    private async Task<ServerOutcome> EvaluateServerAsync(
        Guid serverId,
        Instant now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var outcome = new ServerOutcome();
        try
        {
            await LockServerAsync(serverId, cancellationToken).ConfigureAwait(false);
            var server = await dbContext.GameServers
                .SingleOrDefaultAsync(q => q.PrimaryKey == serverId, cancellationToken)
                .ConfigureAwait(false);
            if (server is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return outcome;
            }

            var settings = await dbContext.ServerAutomationSettings
                .AsNoTracking()
                .Include(q => q.Schedules)
                .SingleOrDefaultAsync(q => q.GameServerFk == serverId, cancellationToken)
                .ConfigureAwait(false);
            var deployment = await dbContext.GameServerDeployments
                .AsNoTracking()
                .Where(q => q.GameServerFk == serverId && q.TimeStampResolved == null)
                .OrderByDescending(q => q.TimeStampCreated)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            var pendings = await dbContext.AutomationPendingStops
                .Where(q => q.GameServerFk == serverId && q.Status == EAutoStopStatus.Pending)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // A warning of a deployment that is gone can never be executed.
            foreach (var orphan in pendings.Where(q => q.DeploymentFk != deployment?.PrimaryKey).ToArray())
            {
                await WithdrawAsync(
                        server,
                        orphan,
                        EAutoStopStatus.Superseded,
                        PendingStopPlanner.DeploymentNotRunningReason,
                        now,
                        outcome,
                        cancellationToken)
                    .ConfigureAwait(false);
                pendings.Remove(orphan);
            }

            var open = pendings.FirstOrDefault();
            var running = !server.IsArchived && deployment is { Status: EDeploymentStatus.Running };
            var stopQueued = deployment is not null
                             && await HasStopJobAsync(deployment.PrimaryKey, cancellationToken)
                                 .ConfigureAwait(false);

            var idleArmed = running
                            && settings is { IdleShutdownEnabled: true }
                            && gameDefinitions.GetDefinitions().Any(q => q.ReportsPlayerCount
                                && string.Equals(
                                    q.GameIdentifier,
                                    server.GameIdentifier,
                                    StringComparison.OrdinalIgnoreCase));
            var idle = await AdvanceIdleClockAsync(server, deployment, idleArmed, now, cancellationToken)
                .ConfigureAwait(false);

            var plan = PendingStopPlanner.Plan(new PendingStopPlanInput(
                now,
                _warningLead,
                running,
                deployment?.TimeStampStarted,
                stopQueued,
                idleArmed,
                settings?.IdleShutdownMinutes ?? _options.DefaultIdleMinutes,
                idleArmed ? idle : null,
                settings is { MaxRuntimeEnabled: true },
                settings?.MaxRuntimeHours ?? _options.DefaultMaxRuntimeHours,
                settings?.TimeStampUpdated ?? Instant.MinValue,
                open is null
                    ? null
                    : new PendingStopSnapshot(
                        open.Reason,
                        open.Limit,
                        open.StopsAt,
                        open.ScheduleFk,
                        open.ScheduleOccurrenceAt),
                await NextScheduledStopAsync(settings, serverId, now, cancellationToken).ConfigureAwait(false),
                open is not { Reason: EAutoStopReason.Schedule } || IsScheduleRuleValid(open, settings)));

            var committed = await ApplyAsync(server, deployment, open, plan, now, outcome, cancellationToken)
                .ConfigureAwait(false);
            if (!committed)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
                return new ServerOutcome();
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return outcome;
        }
        catch (HostingConflictException ex)
        {
            // Another lifecycle action is pending; the claim and the job roll back together and the next pass retries.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation(
                "Automatic stop of server {GameServerId} postponed: {Reason}",
                serverId,
                HostingFailureSanitizer.Sanitize(ex.Message));
            return new ServerOutcome();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            logger.LogWarning(
                "Server automation for server {GameServerId} lost a concurrent write and will retry: {Failure}",
                serverId,
                HostingFailureSanitizer.Sanitize(ex.Message));
            return new ServerOutcome();
        }
    }

    /// <summary>
    /// Finds the scheduled stop whose warning window is open, leaving out occurrences that already have a pending stop
    /// or a recorded run.
    /// </summary>
    /// <param name="settings">The settings of the server with their rules, or <see langword="null" />.</param>
    /// <param name="serverId">The server to look up.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>The candidate, or <see langword="null" /> when no window is open.</returns>
    private async Task<PendingStopCandidate?> NextScheduledStopAsync(
        ServerAutomationSettings? settings,
        Guid serverId,
        Instant now,
        CancellationToken cancellationToken)
    {
        if (settings is null || !settings.Schedules.Any(q => q.Action == EServerScheduleAction.Stop))
            return null;

        var until = now + _warningLead;
        var pendings = await dbContext.AutomationPendingStops
            .AsNoTracking()
            .Where(q => q.GameServerFk == serverId
                        && q.ScheduleFk != null
                        && q.ScheduleOccurrenceAt > now
                        && q.ScheduleOccurrenceAt <= until)
            .Select(q => new { q.ScheduleFk, q.ScheduleOccurrenceAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var runs = await dbContext.ServerScheduleRuns
            .AsNoTracking()
            .Where(q => q.GameServerFk == serverId && q.OccurrenceAt > now && q.OccurrenceAt <= until)
            .Select(q => new { q.ScheduleFk, q.OccurrenceAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var handled = pendings
            .Select(q => (q.ScheduleFk!.Value, q.ScheduleOccurrenceAt!.Value))
            .Concat(runs.Select(q => (q.ScheduleFk, q.OccurrenceAt)))
            .ToHashSet();
        return scheduleRunner.NextScheduledStop(settings, now, _warningLead, handled);
    }

    /// <summary>
    /// Tells whether the rule of an open scheduled stop still exists and has not changed since the warning was opened.
    /// </summary>
    /// <remarks>
    /// Saving a changed rule or time zone resets its effective instant, so a rule that became effective after the
    /// warning was created is a different rule and the warning no longer applies.
    /// </remarks>
    /// <param name="open">The open scheduled stop.</param>
    /// <param name="settings">The settings of the server with their rules, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the warning still applies.</returns>
    private static bool IsScheduleRuleValid(AutomationPendingStop open, ServerAutomationSettings? settings)
        => open is { ScheduleFk: { } ruleId, ScheduleOccurrenceAt: { } occurrence }
           && settings?.Schedules.FirstOrDefault(q => q.PrimaryKey == ruleId) is { } rule
           && rule.EffectiveFrom <= open.TimeStampCreated
           && rule.EffectiveFrom <= occurrence;

    /// <summary>
    /// Applies the pure idle clock to the persisted clock state of a server and stores the result, without ever
    /// touching the settings row or its version.
    /// </summary>
    /// <remarks>
    /// The observation is built exactly like the read model's: the player count is read only while idle shutdown is
    /// armed, and a queued, running, retrying or waiting lifecycle job of the deployment counts as activity.
    /// </remarks>
    /// <param name="server">The server whose clock is advanced.</param>
    /// <param name="deployment">The active deployment, or <see langword="null" />.</param>
    /// <param name="armed">Whether idle shutdown applies to the server right now.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>A task resolving to the clock result of this pass.</returns>
    private async Task<IdleClockResult> AdvanceIdleClockAsync(
        GameServer server,
        GameServerDeployment? deployment,
        bool armed,
        Instant now,
        CancellationToken cancellationToken)
    {
        int? playerCount = null;
        var hasActiveJob = false;
        if (armed)
        {
            // The metrics reading and the job lookup are the expensive part, so they run only while armed.
            var metrics = await metricsService
                .EvaluateCurrentAsync(server.PrimaryKey, now, cancellationToken)
                .ConfigureAwait(false);
            playerCount = metrics.PlayerCount;
            hasActiveJob = await HasActiveJobAsync(deployment!.PrimaryKey, cancellationToken).ConfigureAwait(false);
        }

        var state = await dbContext.ServerAutomationStates
            .SingleOrDefaultAsync(q => q.GameServerFk == server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        var previous = state is null
            ? IdleClockState.Empty
            : new IdleClockState(state.IdleDeploymentFk, state.IdleSince, state.IdleHeldSince);
        var result = IdleClock.Advance(
            previous,
            new IdleObservation(armed, deployment?.PrimaryKey, playerCount, hasActiveJob),
            now);
        if (result.State == previous)
            return result;

        if (state is null)
        {
            state = new ServerAutomationState
            {
                PrimaryKey = Guid.NewGuid(),
                GameServerFk = server.PrimaryKey,
            };
            dbContext.ServerAutomationStates.Add(state);
        }

        state.IdleDeploymentFk = result.State.IdleDeploymentId;
        state.IdleSince = result.State.IdleSince;
        state.IdleHeldSince = result.State.IdleHeldSince;
        state.TimeStampUpdated = now;
        return result;
    }

    /// <summary>
    /// Applies the planner's decision to the database context.
    /// </summary>
    /// <param name="server">The tracked server.</param>
    /// <param name="deployment">The active deployment, or <see langword="null" />.</param>
    /// <param name="open">The tracked open warning, or <see langword="null" />.</param>
    /// <param name="plan">The planner's decision.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="outcome">The collector of alerts and queued jobs of this server.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="false" /> when a concurrent pass already claimed the stop and nothing must be saved.</returns>
    private async Task<bool> ApplyAsync(
        GameServer server,
        GameServerDeployment? deployment,
        AutomationPendingStop? open,
        PendingStopPlan plan,
        Instant now,
        ServerOutcome outcome,
        CancellationToken cancellationToken)
    {
        switch (plan.Decision)
        {
            case EPendingStopDecision.Warn:
                await WarnAsync(server, deployment!, plan.Candidate!, now, outcome, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case EPendingStopDecision.Replace:
            {
                await WithdrawAsync(
                        server,
                        open!,
                        EAutoStopStatus.Superseded,
                        plan.CancelReason!,
                        now,
                        outcome,
                        cancellationToken)
                    .ConfigureAwait(false);
                // The superseded row must leave the unique pending index before the new one enters it.
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                if (plan.Candidate!.StopsAt > now)
                {
                    await WarnAsync(server, deployment!, plan.Candidate, now, outcome, cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var due = NewPendingStop(deployment!, plan.Candidate, now, warnedAt: null);
                dbContext.AutomationPendingStops.Add(due);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await ExecuteAsync(server, due, plan.Candidate, now, DowntimeNote, outcome, cancellationToken)
                    .ConfigureAwait(false);
            }
            case EPendingStopDecision.Cancel:
                await WithdrawAsync(
                        server,
                        open!,
                        EAutoStopStatus.Cancelled,
                        plan.CancelReason!,
                        now,
                        outcome,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case EPendingStopDecision.Supersede:
                await WithdrawAsync(
                        server,
                        open!,
                        EAutoStopStatus.Superseded,
                        plan.CancelReason!,
                        now,
                        outcome,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case EPendingStopDecision.Execute:
                // A stop that never had a warning (committed by the schedule scan) came due unobserved.
                return await ExecuteAsync(
                        server,
                        open!,
                        plan.Candidate!,
                        now,
                        open!.WarnedAt is null ? DowntimeNote : null,
                        outcome,
                        cancellationToken)
                    .ConfigureAwait(false);
            case EPendingStopDecision.ExecuteWithoutWarning:
            {
                var stop = NewPendingStop(deployment!, plan.Candidate!, now, warnedAt: null);
                dbContext.AutomationPendingStops.Add(stop);
                // The row must exist before it can be claimed.
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await ExecuteAsync(server, stop, plan.Candidate!, now, DowntimeNote, outcome, cancellationToken)
                    .ConfigureAwait(false);
            }
            case EPendingStopDecision.None:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Decision, "Unknown pending stop decision.");
        }
    }

    /// <summary>
    /// Opens a warning episode: one pending stop, one alert with its deliveries and one audit entry.
    /// </summary>
    /// <param name="server">The tracked server.</param>
    /// <param name="deployment">The active deployment.</param>
    /// <param name="candidate">The stop to warn about.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="outcome">The collector of alerts and queued jobs of this server.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>A task completing after the rows were added to the context.</returns>
    private async Task WarnAsync(
        GameServer server,
        GameServerDeployment deployment,
        PendingStopCandidate candidate,
        Instant now,
        ServerOutcome outcome,
        CancellationToken cancellationToken)
    {
        var stop = NewPendingStop(deployment, candidate, now, warnedAt: now);
        dbContext.AutomationPendingStops.Add(stop);
        outcome.Raised.Add(await alertWriter
            .AddWarningAsync(stop, deployment, server.Title, now, cancellationToken)
            .ConfigureAwait(false));
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.AutomaticStopWarned,
            "Automatic stop warning opened.",
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: $"Reason={stop.Reason}; Limit={FormatLimit(stop.Limit)}; "
                     + $"StopsAt={stop.StopsAt.ToString("g", CultureInfo.InvariantCulture)}",
            costImpact: ECostImpact.NoCostImpact);
    }

    /// <summary>
    /// Claims a due stop and queues the ordinary stop job in the same transaction.
    /// </summary>
    /// <param name="server">The tracked server.</param>
    /// <param name="stop">The tracked pending stop that is due.</param>
    /// <param name="candidate">The facts of the stop.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="auditNote">An optional note for the queue audit entry.</param>
    /// <param name="outcome">The collector of alerts and queued jobs of this server.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="false" /> when the claim affected no row because the stop is no longer pending.</returns>
    /// <exception cref="HostingConflictException">A lifecycle precondition is not met; the caller rolls back.</exception>
    private async Task<bool> ExecuteAsync(
        GameServer server,
        AutomationPendingStop stop,
        PendingStopCandidate candidate,
        Instant now,
        string? auditNote,
        ServerOutcome outcome,
        CancellationToken cancellationToken)
    {
        var stopId = stop.PrimaryKey;
        var affected = await dbContext.AutomationPendingStops
            .Where(q => q.PrimaryKey == stopId && q.Status == EAutoStopStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(q => q.Status, EAutoStopStatus.Executed)
                    .SetProperty(q => q.ResolvedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
            return false;

        var job = await lifecycleService
            .EnqueueAutomatedActionAsync(
                new AutomatedLifecycleRequest(
                    server.PrimaryKey,
                    GameServerActions.Stop,
                    ToTrigger(candidate.Reason),
                    candidate.Limit,
                    now,
                    auditNote),
                cancellationToken)
            .ConfigureAwait(false);

        // The tracked change keeps the live update frame and the in-memory row consistent with the claim.
        stop.Status = EAutoStopStatus.Executed;
        stop.ResolvedAt = now;
        stop.LifecycleJobFk = job.PrimaryKey;
        if (candidate is { Reason: EAutoStopReason.Schedule, ScheduleId: { } scheduleId, ScheduleOccurrenceAt: { } occurrence }
            && !await ScheduledRunLedger.ExistsAsync(dbContext, scheduleId, occurrence, cancellationToken)
                .ConfigureAwait(false))
        {
            // The run row commits with the claim and the job, so the occurrence acts exactly once.
            ScheduledRunLedger.AddRun(
                dbContext,
                scheduleId,
                server.PrimaryKey,
                occurrence,
                EServerScheduleRunOutcome.Stopped,
                job.PrimaryKey,
                auditNote,
                now);
        }

        if (await alertWriter.ResolveAsync(stopId, now, cancellationToken).ConfigureAwait(false) is { } resolved)
            outcome.Resolved.Add(resolved);
        outcome.JobsQueued++;
        return true;
    }

    /// <summary>
    /// Withdraws an open warning: it is cancelled or superseded, its alert is resolved and the audit entry is written.
    /// </summary>
    /// <param name="server">The tracked server.</param>
    /// <param name="stop">The tracked pending stop.</param>
    /// <param name="status">The terminal status, cancelled or superseded.</param>
    /// <param name="reason">The machine-readable reason.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="outcome">The collector of alerts of this server.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>A task completing after the changes were added to the context.</returns>
    private async Task WithdrawAsync(
        GameServer server,
        AutomationPendingStop stop,
        EAutoStopStatus status,
        string reason,
        Instant now,
        ServerOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (await PendingStopWithdrawal
                .ApplyAsync(dbContext, alertWriter, auditWriter, stop, status, reason, now, cancellationToken)
                .ConfigureAwait(false) is { } resolved)
        {
            outcome.Resolved.Add(resolved);
        }
    }

    private static AutomationPendingStop NewPendingStop(
        GameServerDeployment deployment,
        PendingStopCandidate candidate,
        Instant now,
        Instant? warnedAt)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = deployment.GameServerFk,
            DeploymentFk = deployment.PrimaryKey,
            Reason = candidate.Reason,
            Limit = candidate.Limit,
            ScheduleFk = candidate.ScheduleId,
            ScheduleOccurrenceAt = candidate.ScheduleOccurrenceAt,
            WarnedAt = warnedAt,
            StopsAt = candidate.StopsAt,
            Status = EAutoStopStatus.Pending,
            TimeStampCreated = now,
        };

    private static ELifecycleJobTrigger ToTrigger(EAutoStopReason reason)
        => reason switch
        {
            EAutoStopReason.Idle => ELifecycleJobTrigger.Idle,
            EAutoStopReason.MaxRuntime => ELifecycleJobTrigger.MaxRuntime,
            EAutoStopReason.Schedule => ELifecycleJobTrigger.Schedule,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown automatic stop reason."),
        };

    private static string FormatLimit(int? limit)
        => limit?.ToString(CultureInfo.InvariantCulture) ?? "none";

    /// <summary>
    /// Tells whether a stop job of the deployment is queued, running, waiting, or failed.
    /// </summary>
    /// <remarks>
    /// A failed stop needs a person to retry or skip it. Counting it keeps the evaluator from queuing another stop
    /// every pass and from escalating a failed automatic stop (D-AUTO-2).
    /// </remarks>
    /// <param name="deploymentId">The deployment to look up.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A task resolving to <see langword="true" /> when such a stop job exists.</returns>
    private Task<bool> HasStopJobAsync(Guid deploymentId, CancellationToken cancellationToken)
        => dbContext.LifecycleJobs
            .AsNoTracking()
            .AnyAsync(
                q => q.DeploymentFk == deploymentId
                     && q.Kind == ELifecycleJobKind.Stop
                     && (q.Status == ELifecycleJobStatus.Queued
                         || q.Status == ELifecycleJobStatus.Running
                         || q.Status == ELifecycleJobStatus.RetryPending
                         || q.Status == ELifecycleJobStatus.WaitingForWorker
                         || q.Status == ELifecycleJobStatus.Failed),
                cancellationToken);

    /// <summary>
    /// Tells whether any lifecycle job of the deployment is queued, running, waiting, or retrying.
    /// </summary>
    /// <param name="deploymentId">The deployment to look up.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A task resolving to <see langword="true" /> when such a job exists.</returns>
    private Task<bool> HasActiveJobAsync(Guid deploymentId, CancellationToken cancellationToken)
        => dbContext.LifecycleJobs
            .AsNoTracking()
            .AnyAsync(
                q => q.DeploymentFk == deploymentId
                     && (q.Status == ELifecycleJobStatus.Queued
                         || q.Status == ELifecycleJobStatus.Running
                         || q.Status == ELifecycleJobStatus.RetryPending
                         || q.Status == ELifecycleJobStatus.WaitingForWorker),
                cancellationToken);

    /// <summary>
    /// Takes the durable server-row lock that every lifecycle mutation of the server serializes on.
    /// </summary>
    /// <param name="serverId">The server to lock.</param>
    /// <param name="cancellationToken">A token that cancels the statement.</param>
    /// <returns>A task completing once the lock is held until the transaction ends.</returns>
    private Task LockServerAsync(Guid serverId, CancellationToken cancellationToken)
        => ServerRowLock.AcquireAsync(dbContext, serverId, cancellationToken);

    private bool IsPostgreSql
        => ServerRowLock.IsPostgreSql(dbContext);

    /// <summary>
    /// Collects what one server's transaction raised, withdrew and queued, to be reported only after it committed.
    /// </summary>
    private sealed class ServerOutcome
    {
        /// <summary>
        /// Gets the warning alerts the transaction recorded.
        /// </summary>
        /// <value>The recorded alerts.</value>
        public List<RaisedCostAlert> Raised { get; } = [];

        /// <summary>
        /// Gets the warning alerts the transaction withdrew.
        /// </summary>
        /// <value>The withdrawn alerts.</value>
        public List<ResolvedCostAlert> Resolved { get; } = [];

        /// <summary>
        /// Gets or sets the number of stop jobs the transaction queued.
        /// </summary>
        /// <value>The queued job count.</value>
        public int JobsQueued { get; set; }
    }
}
