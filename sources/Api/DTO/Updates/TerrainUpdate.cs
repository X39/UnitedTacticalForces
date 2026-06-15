namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents an update operation for a terrain, allowing modification of its title and gallery image.
/// </summary>
public record TerrainUpdate
{
    /// <summary>
    /// Represents the title of the terrain.
    /// This property is used as a descriptive name or identifier for the terrain in the application.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gallery media asset used as terrain image.
    /// </summary>
    public Guid? ImageAssetFk { get; init; }
}
