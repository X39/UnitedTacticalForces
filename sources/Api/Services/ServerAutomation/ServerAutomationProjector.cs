using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Projects the automation read model of a game server from persisted state, the pure idle clock, the schedule
/// calculator and the current server metrics.
/// </summary>
/// <remarks>
/// The projector reads and never writes: it applies the idle clock in memory and does not save the result. The
/// automation evaluator uses the same <see cref="IdleClock" />, so the displayed idle state and the evaluator's
/// decision agree.
/// </remarks>
/// <param name="dbContext">The database context the settings are read from.</param>
/// <param name="gameDefinitions">The catalog that tells which games report player counts.</param>
/// <param name="metricsService">The metrics service that supplies the current player count.</param>
/// <param name="authorizationService">The hosting authorization rules.</param>
/// <param name="options">The automation limits and timings.</param>
[Scoped<ServerAutomationProjector, IServerAutomationProjector>]
internal sealed class ServerAutomationProjector(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IServerMetricsService metricsService,
    IHostingAuthorizationService authorizationService,
    IOptions<ServerAutomationOptions> options) : IServerAutomationProjector
{
    private readonly ServerAutomationOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<bool> CanManageAsync(
        ClaimsPrincipal principal,
        GameServer server,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(server);
        if (!authorizationService.HasServerPermission(
                principal,
                server.PrimaryKey,
                Claims.ResourceBased.Server.Modify))
        {
            return false;
        }

        return await authorizationService
            .CanUseCostOwnerAsync(principal, server.CurrentCostOwnerFk, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ServerAutomationDto?> ProjectAsync(
        GameServer server,
        Instant now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        var definition = gameDefinitions.GetDefinitions()
            .SingleOrDefault(q => string.Equals(
                q.GameIdentifier,
                server.GameIdentifier,
                StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return null;
        }

        var settings = await dbContext.ServerAutomationSettings
            .AsNoTracking()
            .Include(q => q.Schedules)
            .SingleOrDefaultAsync(q => q.GameServerFk == server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        var deployment = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.GameServerFk == server.PrimaryKey && q.TimeStampResolved == null)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var pendingStop = deployment is null
            ? null
            : await dbContext.AutomationPendingStops
                .AsNoTracking()
                .Where(q => q.DeploymentFk == deployment.PrimaryKey && q.Status == EAutoStopStatus.Pending)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        var rules = (settings?.Schedules ?? [])
            .OrderBy(q => q.Position)
            .ThenBy(q => q.TimeStampCreated)
            .ToArray();

        var consent = ProjectConsent(server, settings, rules);
        var zone = ScheduleCalculator.FindZone(settings?.TimeZone);
        var schedules = new List<ServerScheduleDto>(rules.Length);
        foreach (var rule in rules)
        {
            var lastRun = await ProjectLastRunAsync(rule, cancellationToken).ConfigureAwait(false);
            // A start rule without consent does not run, and no zone means no calculable occurrence.
            var canRun = zone is not null
                         && !(rule.Action == EServerScheduleAction.Start && consent == EStartConsentState.Needed);
            schedules.Add(new ServerScheduleDto
            {
                Id = rule.PrimaryKey,
                Action = rule.Action,
                Days = ScheduleCalculator.FromDaysMask(rule.DaysMask),
                Time = FormatTime(rule.MinuteOfDay),
                // A rule never fires before it became effective.
                NextRunAt = canRun
                    ? ScheduleCalculator.NextAfter(
                        rule.DaysMask,
                        rule.MinuteOfDay,
                        zone!,
                        Instant.Max(now, rule.EffectiveFrom))
                    : null,
                LastRun = lastRun,
            });
        }

        var next = schedules
            .Where(q => q.NextRunAt is not null)
            .OrderBy(q => q.NextRunAt)
            .FirstOrDefault();
        var status = new ServerAutomationStatusDto
        {
            Idle = await ProjectIdleAsync(server, settings, deployment, pendingStop, definition, now, cancellationToken)
                .ConfigureAwait(false),
            MaxRuntimeStopsAt = ProjectMaxRuntimeStop(settings, deployment, pendingStop, now),
            NextScheduled = next is { NextRunAt: { } at }
                ? new ServerNextScheduledDto { Action = next.Action, At = at }
                : null,
            PendingStop = pendingStop is null
                ? null
                : new ServerPendingStopDto
                {
                    Reason = pendingStop.Reason,
                    StopsAt = pendingStop.StopsAt,
                    Limit = pendingStop.Limit,
                },
        };
        return new ServerAutomationDto
        {
            Settings = new ServerAutomationSettingsDto
            {
                IdleShutdownEnabled = settings?.IdleShutdownEnabled ?? false,
                IdleShutdownMinutes = settings?.IdleShutdownMinutes ?? _options.DefaultIdleMinutes,
                MaxRuntimeEnabled = settings?.MaxRuntimeEnabled ?? false,
                MaxRuntimeHours = settings?.MaxRuntimeHours ?? _options.DefaultMaxRuntimeHours,
                TimeZone = settings?.TimeZone,
                Schedules = schedules,
                Version = settings?.Version ?? 0,
            },
            Limits = new ServerAutomationLimitsDto
            {
                IdleMinutesMin = _options.IdleMinutesMin,
                IdleMinutesMax = _options.IdleMinutesMax,
                MaxRuntimeHoursMin = _options.MaxRuntimeHoursMin,
                MaxRuntimeHoursMax = _options.MaxRuntimeHoursMax,
                MaxSchedules = _options.MaxSchedules,
                WarningLeadMinutes = (int) Math.Ceiling(_options.WarningLead.TotalMinutes),
            },
            IdleShutdown = new ServerIdleShutdownCapabilityDto { Available = definition.ReportsPlayerCount },
            Status = status,
            StartConsent = new ServerStartConsentDto { State = consent },
        };
    }

    private static EStartConsentState ProjectConsent(
        GameServer server,
        ServerAutomationSettings? settings,
        IReadOnlyCollection<ServerSchedule> rules)
    {
        if (rules.All(q => q.Action != EServerScheduleAction.Start))
        {
            return EStartConsentState.NotRequired;
        }

        // The consent is valid only while the owner it was given for still pays for the server.
        return settings?.StartConsentCostOwnerFk == server.CurrentCostOwnerFk
            ? EStartConsentState.Given
            : EStartConsentState.Needed;
    }

    private async Task<ServerScheduleLastRunDto?> ProjectLastRunAsync(
        ServerSchedule rule,
        CancellationToken cancellationToken)
    {
        var last = await dbContext.ServerScheduleRuns
            .AsNoTracking()
            .Where(q => q.ScheduleFk == rule.PrimaryKey)
            .OrderByDescending(q => q.OccurrenceAt)
            .Select(q => new
            {
                q.OccurrenceAt,
                q.Outcome,
                JobStatus = q.LifecycleJob == null ? (ELifecycleJobStatus?) null : q.LifecycleJob.Status,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return last is null
            ? null
            : new ServerScheduleLastRunDto
            {
                At = last.OccurrenceAt,
                // Failed is derived from the job and never stored by the evaluator.
                Outcome = last.JobStatus == ELifecycleJobStatus.Failed
                    ? EServerScheduleRunOutcome.Failed
                    : last.Outcome,
            };
    }

    private async Task<ServerIdleStatusDto> ProjectIdleAsync(
        GameServer server,
        ServerAutomationSettings? settings,
        GameServerDeployment? deployment,
        AutomationPendingStop? pendingStop,
        GameDefinition definition,
        Instant now,
        CancellationToken cancellationToken)
    {
        var armed = settings is { IdleShutdownEnabled: true }
                    && definition.ReportsPlayerCount
                    && deployment is { Status: EDeploymentStatus.Running };
        int? playerCount = null;
        var hasActiveJob = false;
        if (armed)
        {
            // The metrics reading and the job lookup are the expensive part, so they run only when idle shutdown
            // is armed.
            var metrics = await metricsService
                .EvaluateCurrentAsync(server.PrimaryKey, now, cancellationToken)
                .ConfigureAwait(false);
            playerCount = metrics.PlayerCount;
            var deploymentId = deployment!.PrimaryKey;
            hasActiveJob = await dbContext.LifecycleJobs
                .AsNoTracking()
                .AnyAsync(
                    q => q.DeploymentFk == deploymentId
                         && (q.Status == ELifecycleJobStatus.Queued
                             || q.Status == ELifecycleJobStatus.Running
                             || q.Status == ELifecycleJobStatus.RetryPending
                             || q.Status == ELifecycleJobStatus.WaitingForWorker),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var state = await dbContext.ServerAutomationStates
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.GameServerFk == server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        var previous = state is null
            ? IdleClockState.Empty
            : new IdleClockState(state.IdleDeploymentFk, state.IdleSince, state.IdleHeldSince);
        // The same pure transition the evaluator applies; the result is displayed and never saved.
        var result = IdleClock.Advance(
            previous,
            new IdleObservation(armed, deployment?.PrimaryKey, playerCount, hasActiveJob),
            now);

        Instant? stopsAt = null;
        if (result.DisplayState == EAutomationIdleState.Idle
            && result.State.IdleSince is { } since
            && settings is not null)
        {
            var limit = since + Duration.FromMinutes(settings.IdleShutdownMinutes);
            stopsAt = pendingStop is { Reason: EAutoStopReason.Idle }
                ? pendingStop.StopsAt
                : Instant.Max(limit, now) + Duration.FromTimeSpan(_options.WarningLead);
        }

        return new ServerIdleStatusDto
        {
            State = result.DisplayState,
            Since = result.State.IdleSince,
            StopsAt = stopsAt,
        };
    }

    private Instant? ProjectMaxRuntimeStop(
        ServerAutomationSettings? settings,
        GameServerDeployment? deployment,
        AutomationPendingStop? pendingStop,
        Instant now)
    {
        if (settings is not { MaxRuntimeEnabled: true }
            || deployment is not { Status: EDeploymentStatus.Running, TimeStampStarted: { } started })
        {
            return null;
        }

        if (pendingStop is { Reason: EAutoStopReason.MaxRuntime })
        {
            return pendingStop.StopsAt;
        }

        var limit = started + Duration.FromHours(settings.MaxRuntimeHours);
        return Instant.Max(limit, now) + Duration.FromTimeSpan(_options.WarningLead);
    }

    private static string FormatTime(int minuteOfDay)
        => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
}
