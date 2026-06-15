namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Aggregate read model for Command Center asset control surfaces.
/// </summary>
public record CommandCenterAssetsSnapshotDto
{
    /// <summary>
    /// Whether the current user can manage mod packs.
    /// </summary>
    public bool CanManageModPacks { get; init; }

    /// <summary>
    /// Whether the current user can manage terrains.
    /// </summary>
    public bool CanManageTerrains { get; init; }

    /// <summary>
    /// Total active mod packs visible to the asset control surface.
    /// </summary>
    public long? ModPackCount { get; init; }

    /// <summary>
    /// Total active terrains visible to the asset control surface.
    /// </summary>
    public long? TerrainCount { get; init; }

    /// <summary>
    /// Recent or searched mod pack definitions.
    /// </summary>
    public FullModPackDefinitionDto[] ModPacks { get; init; } = [];

    /// <summary>
    /// Recent or searched terrain records.
    /// </summary>
    public PlainTerrainDto[] Terrains { get; init; } = [];
}
