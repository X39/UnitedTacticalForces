namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a stored field report image uploaded through Command Center.
/// </summary>
public record FieldReportImageUploadDto
{
    /// <summary>
    /// Uploaded media asset id.
    /// </summary>
    public Guid ImageAssetFk { get; init; }

    /// <summary>
    /// API-relative path that can be used as a field report hero image path.
    /// </summary>
    public string ImagePath { get; init; } = string.Empty;

    /// <summary>
    /// Normalized image MIME type.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// Size of the stored image in bytes.
    /// </summary>
    public long Size { get; init; }
}
