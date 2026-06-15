namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Represents a data transfer object for creating terrain information.
/// </summary>
public record TerrainCreationPayload
{
    /// <summary>
    /// Gets or sets the title of the terrain.
    /// </summary>
    /// <remarks>
    /// This property represents the name or title associated with a terrain object.
    /// It is used to identify and refer to a terrain instance.
    /// </remarks>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gallery media asset used as terrain image.
    /// </summary>
    public Guid ImageAssetFk { get; set; }
}
