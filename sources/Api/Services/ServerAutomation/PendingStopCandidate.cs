using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// A stop the planner proposes to warn about or to execute, with the facts the evaluator persists.
/// </summary>
/// <param name="Reason">Why the stop applies.</param>
/// <param name="Limit">The limit that applied (minutes for idle, hours for maximum runtime), or
/// <see langword="null" /> for a schedule stop.</param>
/// <param name="StopsAt">The instant the stop is requested.</param>
/// <param name="ScheduleId">The schedule rule that caused the stop, or <see langword="null" />.</param>
/// <param name="ScheduleOccurrenceAt">The occurrence instant of the schedule rule, or <see langword="null" />.</param>
public sealed record PendingStopCandidate(
    EAutoStopReason Reason,
    int? Limit,
    Instant StopsAt,
    Guid? ScheduleId,
    Instant? ScheduleOccurrenceAt);
