namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents an update operation for a terrain, allowing modification of its title, image, and image MIME type.
/// </summary>
public record TerrainUpdate
{
    /// <summary>
    /// Represents the title of the terrain.
    /// This property is used as a descriptive name or identifier for the terrain in the application.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Represents the binary data of an image associated with a terrain.
    /// This property stores the image in a byte array format, which can be utilized for rendering,
    /// saving, or manipulation of the terrain's visual representation within the application.
    /// </summary>
    public byte[]? Image { get; init; }

    /// <summary>
    /// Represents the MIME type associated with an image.
    /// This property is used to specify the format of the image (e.g., "image/jpeg", "image/png")
    /// for proper handling and processing within the application.
    /// </summary>
    public string? ImageMimeType { get; init; }
}
