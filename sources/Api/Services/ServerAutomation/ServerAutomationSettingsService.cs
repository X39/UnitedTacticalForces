using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Database-backed saving of the automation settings and weekly schedule rules of a game server.
/// </summary>
/// <remarks>
/// <para>
/// A save is authorized by the route id alone: a caller who may not view the server learns nothing (not found), a
/// viewer without <see cref="IServerAutomationProjector.CanManageAsync" /> is forbidden. A save replaces the whole
/// configuration inside one transaction. The stored version is claimed with a conditional update, so two saves based
/// on the same version cannot both succeed even across API instances; the first save of a server is guarded by the
/// unique index on the server.
/// </para>
/// <para>
/// A new or changed rule becomes effective at the save time, and the first save creates the evaluator state with its
/// schedule watermark at the save time, so saving can never fire an occurrence from the past. The service never
/// writes evaluator state other than creating the missing state row.
/// </para>
/// <para>
/// Scheduled starts cost money, so a Start rule needs the recorded consent of the cost owner that currently pays for
/// the server. Adding or changing a Start rule without consent clears the stored consent.
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context the settings are stored in.</param>
/// <param name="authorizationService">The hosting authorization rules.</param>
/// <param name="projector">The projector that decides the manage right and builds the response block.</param>
/// <param name="gameDefinitions">The catalog that tells which games report player counts.</param>
/// <param name="auditWriter">The writer that adds append-only audit evidence to the save transaction.</param>
/// <param name="options">The limits on idle time, runtime and the number of schedule rules.</param>
/// <param name="logger">The logger that records saves by server id, version and user id only.</param>
[Scoped<ServerAutomationSettingsService, IServerAutomationSettingsService>]
internal sealed class ServerAutomationSettingsService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    IServerAutomationProjector projector,
    IGameServerDefinitionCatalog gameDefinitions,
    IHostingAuditWriter auditWriter,
    IOptions<ServerAutomationOptions> options,
    ILogger<ServerAutomationSettingsService> logger) : IServerAutomationSettingsService
{
    /// <summary>
    /// The longest time zone identifier the settings can store.
    /// </summary>
    private const int MaxTimeZoneLength = 64;

    /// <summary>
    /// The message of a conflict caused by a stale version or a concurrent save.
    /// </summary>
    private const string ConflictMessage = "The automation settings changed; reload them and try again.";

    /// <inheritdoc />
    public async Task<ServerAutomationDto> UpdateAsync(
        Guid gameServerId,
        ServerAutomationUpdate update,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(principal);
        if (!principal.TryGetUserId(out var userId))
            throw new HostingForbiddenException("A signed-in user is required.");

        // A caller who may not view the server must not learn whether it exists.
        if (!authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.View))
            throw new HostingNotFoundException("The game server was not found.");
        var server = await dbContext.GameServers
                         .AsNoTracking()
                         .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId, cancellationToken)
                         .ConfigureAwait(false)
                     ?? throw new HostingNotFoundException("The game server was not found.");
        if (server.IsArchived)
            throw new HostingConflictException("The server is archived.");
        if (!await projector.CanManageAsync(principal, server, cancellationToken).ConfigureAwait(false))
            throw new HostingForbiddenException("The automation of this server may not be changed.");

        var definition = gameDefinitions.GetDefinitions()
                             .SingleOrDefault(q => string.Equals(
                                 q.GameIdentifier,
                                 server.GameIdentifier,
                                 StringComparison.OrdinalIgnoreCase))
                         ?? throw new HostingValidationException("The game of the server is not known.");
        var stored = await dbContext.ServerAutomationSettings
            .AsNoTracking()
            .Include(q => q.Schedules)
            .SingleOrDefaultAsync(q => q.GameServerFk == gameServerId, cancellationToken)
            .ConfigureAwait(false);
        var normalized = Validate(update, definition.ReportsPlayerCount, stored);

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var version = await SaveAsync(server, update, normalized, userId, now, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Automation settings of game server {GameServerId} saved as version {Version} by user {UserId}.",
            gameServerId,
            version,
            userId);
        return await projector.ProjectAsync(server, now, cancellationToken).ConfigureAwait(false)
               ?? throw new HostingValidationException("The game of the server is not known.");
    }

    /// <summary>
    /// Validates an update against the configured limits and the capabilities of the game, collecting every failing
    /// field.
    /// </summary>
    /// <param name="update">The update to validate.</param>
    /// <param name="reportsPlayerCount">Whether the game of the server reports player counts.</param>
    /// <param name="stored">The stored settings with their rules, or <see langword="null" /> before the first save.</param>
    /// <returns>The normalized content to store.</returns>
    /// <exception cref="HostingFieldValidationException">The update violates a rule; nothing may be stored.</exception>
    private NormalizedAutomation Validate(
        ServerAutomationUpdate update,
        bool reportsPlayerCount,
        ServerAutomationSettings? stored)
    {
        var limits = options.Value;
        var fields = new List<string>();

        var idleMinutes = update.IdleShutdownMinutes;
        var idleInRange = idleMinutes >= limits.IdleMinutesMin && idleMinutes <= limits.IdleMinutesMax;
        if (update.IdleShutdownEnabled)
        {
            if (!reportsPlayerCount)
                AddField(fields, "idleShutdownEnabled");
            if (!idleInRange)
                AddField(fields, "idleShutdownMinutes");
        }
        else if (!idleInRange)
        {
            // A disabled value is never rejected; the stored number survives so switching the limit back on keeps it.
            idleMinutes = stored?.IdleShutdownMinutes ?? limits.DefaultIdleMinutes;
        }

        var runtimeHours = update.MaxRuntimeHours;
        var runtimeInRange = runtimeHours >= limits.MaxRuntimeHoursMin && runtimeHours <= limits.MaxRuntimeHoursMax;
        if (update.MaxRuntimeEnabled)
        {
            if (!runtimeInRange)
                AddField(fields, "maxRuntimeHours");
        }
        else if (!runtimeInRange)
        {
            runtimeHours = stored?.MaxRuntimeHours ?? limits.DefaultMaxRuntimeHours;
        }

        string? timeZone = null;
        if (!string.IsNullOrWhiteSpace(update.TimeZone))
        {
            // Only IANA ids known to the time zone database are stored, in their canonical spelling.
            var zone = update.TimeZone.Length <= MaxTimeZoneLength
                ? ScheduleCalculator.FindZone(update.TimeZone)
                : null;
            if (zone is null)
                AddField(fields, "timeZone");
            else
                timeZone = zone.Id;
        }

        var rules = ValidateRules(update, timeZone is not null, stored, fields);
        if (update.StartConsent && rules.All(q => q.Action != EServerScheduleAction.Start))
            AddField(fields, "startConsent");

        if (fields.Count > 0)
            throw new HostingFieldValidationException("The automation settings are invalid.", fields);
        return new NormalizedAutomation(
            update.IdleShutdownEnabled,
            idleMinutes,
            update.MaxRuntimeEnabled,
            runtimeHours,
            timeZone,
            rules);
    }

    /// <summary>
    /// Validates the weekly rules of an update.
    /// </summary>
    /// <param name="update">The update whose rules are validated.</param>
    /// <param name="hasTimeZone">Whether the update carries a valid time zone.</param>
    /// <param name="stored">The stored settings with their rules, or <see langword="null" /> before the first save.</param>
    /// <param name="fields">The collected failing fields, extended by every violation found here.</param>
    /// <returns>The rules in payload order; rules that failed validation are left out.</returns>
    private IReadOnlyList<NormalizedRule> ValidateRules(
        ServerAutomationUpdate update,
        bool hasTimeZone,
        ServerAutomationSettings? stored,
        List<string> fields)
    {
        var schedules = update.Schedules ?? [];
        if (schedules.Count > options.Value.MaxSchedules)
            AddField(fields, "schedules");
        if (schedules.Count > 0 && !hasTimeZone)
            AddField(fields, "timeZone");

        var storedIds = (stored?.Schedules ?? []).Select(q => q.PrimaryKey).ToHashSet();
        var seenIds = new HashSet<Guid>();
        var valid = new List<(int Index, NormalizedRule Rule)>(schedules.Count);
        for (var index = 0; index < schedules.Count; index++)
        {
            var rule = schedules[index];
            if (rule is null)
            {
                AddField(fields, $"schedules[{index}]");
                continue;
            }

            var ok = true;
            if (rule.Id is { } id && (!storedIds.Contains(id) || !seenIds.Add(id)))
            {
                AddField(fields, $"schedules[{index}]");
                ok = false;
            }

            if (!Enum.IsDefined(rule.Action))
            {
                AddField(fields, $"schedules[{index}].action");
                ok = false;
            }

            var days = rule.Days ?? [];
            if (days.Count == 0 || days.Any(q => q is < 1 or > 7) || days.Distinct().Count() != days.Count)
            {
                AddField(fields, $"schedules[{index}].days");
                ok = false;
            }

            var minuteOfDay = ParseTime(rule.Time);
            if (minuteOfDay is null)
            {
                AddField(fields, $"schedules[{index}].time");
                ok = false;
            }

            if (ok)
            {
                valid.Add((index, new NormalizedRule(
                    rule.Id,
                    rule.Action,
                    ScheduleCalculator.ToDaysMask(days),
                    minuteOfDay!.Value)));
            }
        }

        // Two rules that fire at the same minute of a shared weekday are redundant; report both.
        for (var i = 0; i < valid.Count; i++)
        {
            for (var j = i + 1; j < valid.Count; j++)
            {
                if (valid[i].Rule.MinuteOfDay != valid[j].Rule.MinuteOfDay
                    || (valid[i].Rule.DaysMask & valid[j].Rule.DaysMask) == 0)
                {
                    continue;
                }

                AddField(fields, $"schedules[{valid[i].Index}].time");
                AddField(fields, $"schedules[{valid[j].Index}].time");
            }
        }

        return valid.Select(q => q.Rule).ToArray();
    }

    /// <summary>
    /// Parses a strict <c>HH:mm</c> time of day.
    /// </summary>
    /// <param name="time">The text to parse.</param>
    /// <returns>The minutes after midnight, or <see langword="null" /> when the text is not a time from 00:00 to 23:59.</returns>
    private static int? ParseTime(string? time)
    {
        if (time is not { Length: 5 } || time[2] != ':')
            return null;
        if (!char.IsAsciiDigit(time[0])
            || !char.IsAsciiDigit(time[1])
            || !char.IsAsciiDigit(time[3])
            || !char.IsAsciiDigit(time[4]))
        {
            return null;
        }

        var hours = (time[0] - '0') * 10 + (time[1] - '0');
        var minutes = (time[3] - '0') * 10 + (time[4] - '0');
        return hours > 23 || minutes > 59 ? null : hours * 60 + minutes;
    }

    /// <summary>
    /// Adds a failing field once.
    /// </summary>
    /// <param name="fields">The collected failing fields.</param>
    /// <param name="field">The path of the failing field.</param>
    private static void AddField(List<string> fields, string field)
    {
        if (!fields.Contains(field))
            fields.Add(field);
    }

    /// <summary>
    /// Stores validated settings and rules in one transaction and writes the audit entries.
    /// </summary>
    /// <param name="server">The server the settings belong to.</param>
    /// <param name="update">The update carrying the base version and the consent flag.</param>
    /// <param name="normalized">The validated content.</param>
    /// <param name="userId">The saving user.</param>
    /// <param name="now">The save instant.</param>
    /// <param name="cancellationToken">A token that cancels the save.</param>
    /// <returns>The version the settings carry after the save.</returns>
    /// <exception cref="HostingConflictException">The version is stale or a concurrent first save won.</exception>
    /// <exception cref="HostingFieldValidationException">A rule id no longer belongs to the server.</exception>
    private async Task<int> SaveAsync(
        GameServer server,
        ServerAutomationUpdate update,
        NormalizedAutomation normalized,
        Guid userId,
        Instant now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var existing = await dbContext.ServerAutomationSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(q => q.GameServerFk == server.PrimaryKey, cancellationToken)
                .ConfigureAwait(false);
            var hasStartRule = normalized.Rules.Any(q => q.Action == EServerScheduleAction.Start);
            int version;
            bool contentChanged;
            bool consentRecorded;
            if (existing is null)
            {
                if (update.Version != 0)
                    throw new HostingConflictException(ConflictMessage);
                version = 1;
                contentChanged = true;
                consentRecorded = update.StartConsent && hasStartRule;
                var created = new ServerAutomationSettings
                {
                    PrimaryKey          = Guid.NewGuid(),
                    GameServerFk        = server.PrimaryKey,
                    IdleShutdownEnabled = normalized.IdleShutdownEnabled,
                    IdleShutdownMinutes = normalized.IdleShutdownMinutes,
                    MaxRuntimeEnabled   = normalized.MaxRuntimeEnabled,
                    MaxRuntimeHours     = normalized.MaxRuntimeHours,
                    TimeZone            = normalized.TimeZone,
                    Version             = version,
                    UpdatedByFk         = userId,
                    TimeStampCreated    = now,
                    TimeStampUpdated    = now,
                };
                if (consentRecorded)
                    RecordConsent(created, server, userId, now);
                dbContext.ServerAutomationSettings.Add(created);
                for (var index = 0; index < normalized.Rules.Count; index++)
                    dbContext.ServerSchedules.Add(CreateRule(created, normalized.Rules[index], index, now));
            }
            else
            {
                if (update.Version != existing.Version)
                    throw new HostingConflictException(ConflictMessage);
                version = existing.Version;
                var storedRules = await dbContext.ServerSchedules
                    .Where(q => q.SettingsFk == existing.PrimaryKey)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var rulesChanged = DiffRules(
                    existing,
                    storedRules,
                    normalized,
                    now,
                    out var startRuleChanged);

                var consentValid = existing.StartConsentCostOwnerFk == server.CurrentCostOwnerFk;
                var clearConsent = startRuleChanged && existing.StartConsentCostOwnerFk is not null;
                // An already valid consent is not recorded again unless a Start rule changed under it.
                consentRecorded = update.StartConsent && hasStartRule && (!consentValid || startRuleChanged);
                contentChanged = rulesChanged
                                 || clearConsent
                                 || existing.IdleShutdownEnabled != normalized.IdleShutdownEnabled
                                 || existing.IdleShutdownMinutes != normalized.IdleShutdownMinutes
                                 || existing.MaxRuntimeEnabled != normalized.MaxRuntimeEnabled
                                 || existing.MaxRuntimeHours != normalized.MaxRuntimeHours
                                 || existing.TimeZone != normalized.TimeZone;

                // The final consent columns: a new consent replaces, a changed Start rule clears, otherwise keep.
                var consentOwner = existing.StartConsentCostOwnerFk;
                var consentBy = existing.StartConsentByFk;
                var consentAt = existing.StartConsentAt;
                var consentEstimate = existing.StartConsentHourlyEstimateEur;
                if (consentRecorded)
                {
                    consentOwner = server.CurrentCostOwnerFk;
                    consentBy = userId;
                    consentAt = now;
                    consentEstimate = server.EstimatedHourlyCostEur;
                }
                else if (clearConsent)
                {
                    consentOwner = null;
                    consentBy = null;
                    consentAt = null;
                    consentEstimate = null;
                }

                if (contentChanged)
                {
                    // The conditional update claims the version and takes the row lock, so a concurrent save waits
                    // and then finds the version it was based on gone.
                    var claimed = await dbContext.ServerAutomationSettings
                        .Where(q => q.PrimaryKey == existing.PrimaryKey && q.Version == update.Version)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(q => q.Version, q => q.Version + 1)
                                .SetProperty(q => q.IdleShutdownEnabled, normalized.IdleShutdownEnabled)
                                .SetProperty(q => q.IdleShutdownMinutes, normalized.IdleShutdownMinutes)
                                .SetProperty(q => q.MaxRuntimeEnabled, normalized.MaxRuntimeEnabled)
                                .SetProperty(q => q.MaxRuntimeHours, normalized.MaxRuntimeHours)
                                .SetProperty(q => q.TimeZone, normalized.TimeZone)
                                .SetProperty(q => q.StartConsentCostOwnerFk, consentOwner)
                                .SetProperty(q => q.StartConsentByFk, consentBy)
                                .SetProperty(q => q.StartConsentAt, consentAt)
                                .SetProperty(q => q.StartConsentHourlyEstimateEur, consentEstimate)
                                .SetProperty(q => q.UpdatedByFk, (Guid?) userId)
                                .SetProperty(q => q.TimeStampUpdated, now),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (claimed == 0)
                        throw new HostingConflictException(ConflictMessage);
                    version = existing.Version + 1;
                }
                else if (consentRecorded)
                {
                    // Recording consent alone is not a content change, so the version stays and an open editor does
                    // not go stale; the version condition still keeps it from racing a content save.
                    var claimed = await dbContext.ServerAutomationSettings
                        .Where(q => q.PrimaryKey == existing.PrimaryKey && q.Version == update.Version)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(q => q.StartConsentCostOwnerFk, consentOwner)
                                .SetProperty(q => q.StartConsentByFk, consentBy)
                                .SetProperty(q => q.StartConsentAt, consentAt)
                                .SetProperty(q => q.StartConsentHourlyEstimateEur, consentEstimate),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (claimed == 0)
                        throw new HostingConflictException(ConflictMessage);
                }
            }

            if (!contentChanged && !consentRecorded)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return version;
            }

            await EnsureStateAsync(server.PrimaryKey, now, cancellationToken).ConfigureAwait(false);
            if (contentChanged)
            {
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.AutomationSettingsChanged,
                    "Automation settings changed.",
                    userId,
                    server.PrimaryKey,
                    costOwnerId: server.CurrentCostOwnerFk,
                    details: $"Idle shutdown {(normalized.IdleShutdownEnabled ? "on" : "off")} "
                             + $"({normalized.IdleShutdownMinutes} min), "
                             + $"maximum runtime {(normalized.MaxRuntimeEnabled ? "on" : "off")} "
                             + $"({normalized.MaxRuntimeHours} h), "
                             + $"{normalized.Rules.Count} schedule rules, version {version}.");
            }

            if (consentRecorded)
            {
                var estimate = server.EstimatedHourlyCostEur?.ToString("0.######", CultureInfo.InvariantCulture)
                               ?? "unknown";
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.ScheduledStartsConfirmed,
                    "Scheduled starts confirmed.",
                    userId,
                    server.PrimaryKey,
                    costOwnerId: server.CurrentCostOwnerFk,
                    details: $"Cost owner {server.CurrentCostOwnerFk}, estimated hourly cost {estimate} EUR.",
                    costImpact: ECostImpact.StartsCost);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return version;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            throw new HostingConflictException(ConflictMessage);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Applies the payload rules to the tracked stored rules: creates, updates, reorders and removes.
    /// </summary>
    /// <param name="existing">The stored settings, with the time zone the rules were saved under.</param>
    /// <param name="storedRules">The tracked stored rules.</param>
    /// <param name="normalized">The validated payload content.</param>
    /// <param name="now">The save instant every new or changed rule becomes effective at.</param>
    /// <param name="startRuleChanged">
    /// Whether a Start rule was added or a rule now starts the server, which invalidates a stored consent.
    /// </param>
    /// <returns><see langword="true" /> when any rule was added, changed, reordered or removed.</returns>
    /// <exception cref="HostingFieldValidationException">A payload rule id is not among the stored rules.</exception>
    private bool DiffRules(
        ServerAutomationSettings existing,
        List<ServerSchedule> storedRules,
        NormalizedAutomation normalized,
        Instant now,
        out bool startRuleChanged)
    {
        startRuleChanged = false;
        var changed = false;
        // Another time zone moves every occurrence, so no rule may fire for an occurrence that predates the save.
        var zoneChanged = existing.TimeZone != normalized.TimeZone;
        var byId = storedRules.ToDictionary(q => q.PrimaryKey);
        var keptIds = new HashSet<Guid>();
        for (var index = 0; index < normalized.Rules.Count; index++)
        {
            var rule = normalized.Rules[index];
            if (rule.Id is not { } id)
            {
                dbContext.ServerSchedules.Add(CreateRule(existing, rule, index, now));
                changed = true;
                startRuleChanged |= rule.Action == EServerScheduleAction.Start;
                continue;
            }

            if (!byId.TryGetValue(id, out var stored))
            {
                // The rule was deleted by a concurrent save after validation.
                throw new HostingFieldValidationException(
                    "The automation settings are invalid.",
                    [$"schedules[{index}]"]);
            }

            keptIds.Add(id);
            var definitionChanged = stored.Action != rule.Action
                                    || stored.DaysMask != rule.DaysMask
                                    || stored.MinuteOfDay != rule.MinuteOfDay;
            if (definitionChanged)
            {
                stored.Action = rule.Action;
                stored.DaysMask = rule.DaysMask;
                stored.MinuteOfDay = rule.MinuteOfDay;
                changed = true;
                startRuleChanged |= rule.Action == EServerScheduleAction.Start;
            }

            if (definitionChanged || zoneChanged)
                stored.EffectiveFrom = now;
            if (stored.Position != index)
            {
                stored.Position = index;
                changed = true;
            }
        }

        var removed = storedRules.Where(q => !keptIds.Contains(q.PrimaryKey)).ToList();
        if (removed.Count > 0)
        {
            // The runs of a removed rule go with it; the audit trail keeps the history.
            dbContext.ServerSchedules.RemoveRange(removed);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Creates a new rule that becomes effective at the save time.
    /// </summary>
    /// <param name="settings">The settings that own the rule.</param>
    /// <param name="rule">The validated rule content.</param>
    /// <param name="position">The display position of the rule.</param>
    /// <param name="now">The save instant, which is also the instant the rule becomes effective.</param>
    /// <returns>The rule to add.</returns>
    private static ServerSchedule CreateRule(
        ServerAutomationSettings settings,
        NormalizedRule rule,
        int position,
        Instant now)
        => new()
        {
            PrimaryKey       = Guid.NewGuid(),
            SettingsFk       = settings.PrimaryKey,
            GameServerFk     = settings.GameServerFk,
            Action           = rule.Action,
            DaysMask         = rule.DaysMask,
            MinuteOfDay      = rule.MinuteOfDay,
            Position         = position,
            EffectiveFrom    = now,
            TimeStampCreated = now,
        };

    /// <summary>
    /// Records the scheduled-start consent on a settings row from the server's current cost owner and estimate.
    /// </summary>
    /// <param name="settings">The settings that receive the consent.</param>
    /// <param name="server">The server whose current cost owner and hourly estimate are recorded.</param>
    /// <param name="userId">The user who confirms.</param>
    /// <param name="now">The confirmation instant.</param>
    private static void RecordConsent(ServerAutomationSettings settings, GameServer server, Guid userId, Instant now)
    {
        settings.StartConsentCostOwnerFk = server.CurrentCostOwnerFk;
        settings.StartConsentByFk = userId;
        settings.StartConsentAt = now;
        settings.StartConsentHourlyEstimateEur = server.EstimatedHourlyCostEur;
    }

    /// <summary>
    /// Creates the evaluator state row of a server when it does not exist yet. An existing row is never touched.
    /// </summary>
    /// <param name="gameServerId">The server the state belongs to.</param>
    /// <param name="now">
    /// The save instant, used as the schedule watermark so the evaluator never replays occurrences from before the
    /// first save.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A task completing once the row is tracked for insertion when it was missing.</returns>
    private async Task EnsureStateAsync(Guid gameServerId, Instant now, CancellationToken cancellationToken)
    {
        if (await dbContext.ServerAutomationStates
                .AnyAsync(q => q.GameServerFk == gameServerId, cancellationToken)
                .ConfigureAwait(false))
            return;
        dbContext.ServerAutomationStates.Add(new ServerAutomationState
        {
            PrimaryKey                = Guid.NewGuid(),
            GameServerFk              = gameServerId,
            SchedulesEvaluatedThrough = now,
            TimeStampUpdated          = now,
        });
    }

    /// <summary>
    /// One validated weekly rule of an automation save.
    /// </summary>
    /// <param name="Id">The identifier of the stored rule, or <see langword="null" /> for a new rule.</param>
    /// <param name="Action">The action the rule performs.</param>
    /// <param name="DaysMask">The weekday bit mask, Monday is bit zero.</param>
    /// <param name="MinuteOfDay">The local time of day in minutes after midnight.</param>
    private sealed record NormalizedRule(Guid? Id, EServerScheduleAction Action, int DaysMask, int MinuteOfDay);

    /// <summary>
    /// The validated, normalized content of an automation save.
    /// </summary>
    /// <param name="IdleShutdownEnabled">Whether idle shutdown is on.</param>
    /// <param name="IdleShutdownMinutes">The idle minutes to store, within range or kept from the stored value.</param>
    /// <param name="MaxRuntimeEnabled">Whether the maximum runtime limit is on.</param>
    /// <param name="MaxRuntimeHours">The runtime hours to store, within range or kept from the stored value.</param>
    /// <param name="TimeZone">The canonical IANA zone to store, or <see langword="null" /> for none.</param>
    /// <param name="Rules">The validated rules in display order.</param>
    private sealed record NormalizedAutomation(
        bool IdleShutdownEnabled,
        int IdleShutdownMinutes,
        bool MaxRuntimeEnabled,
        int MaxRuntimeHours,
        string? TimeZone,
        IReadOnlyList<NormalizedRule> Rules);
}
