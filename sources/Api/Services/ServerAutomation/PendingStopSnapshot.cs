using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The planner's view of an open (pending) automatic stop warning.
/// </summary>
/// <param name="Reason">Why the warning was opened.</param>
/// <param name="Limit">The limit that applied (minutes for idle, hours for maximum runtime), or
/// <see langword="null" /> for a schedule stop.</param>
/// <param name="StopsAt">The instant the stop is requested unless the warning is withdrawn first.</param>
/// <param name="ScheduleId">The schedule rule that caused the warning, or <see langword="null" />.</param>
/// <param name="ScheduleOccurrenceAt">The occurrence instant of the schedule rule, or <see langword="null" />.</param>
public sealed record PendingStopSnapshot(
    EAutoStopReason Reason,
    int? Limit,
    Instant StopsAt,
    Guid? ScheduleId,
    Instant? ScheduleOccurrenceAt);
