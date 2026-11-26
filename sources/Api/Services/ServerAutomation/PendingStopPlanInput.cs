namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Every fact <see cref="PendingStopPlanner" /> needs; the planner reads neither a clock nor a database.
/// </summary>
/// <param name="Now">The evaluation instant.</param>
/// <param name="WarningLead">The time between a warning and its stop.</param>
/// <param name="DeploymentRunning">Whether the server has an active deployment in the running state.</param>
/// <param name="DeploymentStartedAt">The instant the active deployment started, or <see langword="null" />.</param>
/// <param name="StopAlreadyQueued">Whether a stop job of the active deployment is already pending.</param>
/// <param name="IdleArmed">Whether idle shutdown applies: switched on, supported by the game, and running.</param>
/// <param name="IdleMinutes">The configured idle limit in minutes.</param>
/// <param name="Idle">The idle clock result of this pass, or <see langword="null" /> when idle is not armed.</param>
/// <param name="MaxRuntimeEnabled">Whether the maximum runtime limit is switched on.</param>
/// <param name="MaxRuntimeHours">The configured maximum runtime in hours.</param>
/// <param name="MaxRuntimeEffectiveFrom">
/// The instant the settings were saved last; the maximum runtime limit never lies before it, so enabling or lowering
/// the limit on a long-running server warns first instead of stopping at once.
/// </param>
/// <param name="OpenPending">The open warning of the deployment, or <see langword="null" />.</param>
/// <param name="ScheduledStop">
/// The scheduled stop whose warning window is open, or <see langword="null" />.
/// </param>
/// <param name="OpenScheduleRuleValid">
/// Whether the rule of an open scheduled stop still exists unchanged since the warning was opened. It is ignored
/// for every other kind of warning.
/// </param>
public sealed record PendingStopPlanInput(
    Instant Now,
    Duration WarningLead,
    bool DeploymentRunning,
    Instant? DeploymentStartedAt,
    bool StopAlreadyQueued,
    bool IdleArmed,
    int IdleMinutes,
    IdleClockResult? Idle,
    bool MaxRuntimeEnabled,
    int MaxRuntimeHours,
    Instant MaxRuntimeEffectiveFrom,
    PendingStopSnapshot? OpenPending,
    PendingStopCandidate? ScheduledStop,
    bool OpenScheduleRuleValid = true);
