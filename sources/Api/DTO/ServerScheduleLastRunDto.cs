using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The latest recorded occurrence of a schedule rule.
/// </summary>
public sealed record ServerScheduleLastRunDto
{
    /// <summary>
    /// Gets the instant the occurrence was scheduled for.
    /// </summary>
    /// <value>The occurrence instant.</value>
    public required Instant At { get; init; }

    /// <summary>
    /// Gets what the control plane decided or what happened to the job it queued.
    /// </summary>
    /// <value>The recorded outcome, or Failed when the job of the occurrence ended failed.</value>
    public required EServerScheduleRunOutcome Outcome { get; init; }
}
