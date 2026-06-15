namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Aggregate read model for Command Center event administration.
/// </summary>
public record CommandCenterEventAdminSnapshotDto
{
    /// <summary>
    /// Total event count matching the current administration query.
    /// </summary>
    public long EventCount { get; init; }

    /// <summary>
    /// Event administration rows for the current window.
    /// </summary>
    public CommandCenterEventAdminEventDto[] Events { get; init; } = [];

    /// <summary>
    /// Upcoming event summary rows.
    /// </summary>
    public UpcomingEventDto[] UpcomingEvents { get; init; } = [];
}
