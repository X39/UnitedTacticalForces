namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Event administration row with display-ready references for Command Center surfaces.
/// </summary>
public record CommandCenterEventAdminEventDto
{
    /// <summary>
    /// Core event data.
    /// </summary>
    public PlainEventDto PlainEvent { get; init; } = new();

    /// <summary>
    /// Event owner.
    /// </summary>
    public PlainUserDto? Owner { get; init; }

    /// <summary>
    /// Event host.
    /// </summary>
    public PlainUserDto? HostedBy { get; init; }

    /// <summary>
    /// Terrain dependency.
    /// </summary>
    public PlainTerrainDto? Terrain { get; init; }

    /// <summary>
    /// Mod pack revision dependency.
    /// </summary>
    public ModPackRevisionDto? ModPackRevision { get; init; }
}
