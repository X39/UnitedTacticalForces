using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The next scheduled action of a game server.
/// </summary>
public sealed record ServerNextScheduledDto
{
    /// <summary>
    /// Gets the action that runs next.
    /// </summary>
    /// <value>Start or stop.</value>
    public required EServerScheduleAction Action { get; init; }

    /// <summary>
    /// Gets the instant the action runs.
    /// </summary>
    /// <value>The occurrence instant calculated in the schedule time zone.</value>
    public required Instant At { get; init; }
}
