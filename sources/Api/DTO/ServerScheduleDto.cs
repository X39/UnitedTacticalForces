using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One weekly schedule rule of a game server.
/// </summary>
public sealed record ServerScheduleDto
{
    /// <summary>
    /// Gets the identifier of the rule.
    /// </summary>
    /// <value>The primary key of the rule.</value>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the action the rule performs.
    /// </summary>
    /// <value>Start or stop.</value>
    public required EServerScheduleAction Action { get; init; }

    /// <summary>
    /// Gets the weekdays the rule applies to.
    /// </summary>
    /// <value>ISO weekdays in ascending order, Monday is 1 and Sunday is 7.</value>
    public required IReadOnlyList<int> Days { get; init; }

    /// <summary>
    /// Gets the local time of day in the schedule time zone.
    /// </summary>
    /// <value>The time formatted as <c>HH:mm</c>.</value>
    public required string Time { get; init; }

    /// <summary>
    /// Gets the instant the rule runs next, calculated by the control plane.
    /// </summary>
    /// <value>
    /// The next occurrence, or <see langword="null"/> when the schedule has no time zone or the rule cannot run, for
    /// example a start rule without start consent.
    /// </value>
    public Instant? NextRunAt { get; init; }

    /// <summary>
    /// Gets the latest recorded occurrence of the rule.
    /// </summary>
    /// <value>The last run, or <see langword="null"/> when the rule never ran.</value>
    public ServerScheduleLastRunDto? LastRun { get; init; }
}
