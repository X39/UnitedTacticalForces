namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a data transfer object for plain terrain information.
/// </summary>
public record PlainTerrainDto
{
    /// <summary>
    /// Represents the primary key of an entity within the context of the application's data model.
    /// This property is used as a unique identifier for the associated record in the database.
    /// </summary>
    public long PrimaryKey { get; set; }

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

    /// <summary>
    /// API-relative render path for the terrain image.
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

    /// Indicates if the terrain is active or not.
    /// A value of `true` means the terrain is currently active and can be used or accessed.
    /// A value of `false` implies the terrain is inactive, typically representing scenarios
    /// such as being marked for deletion or temporarily disabled.
    public bool IsActive { get; set; }
}
