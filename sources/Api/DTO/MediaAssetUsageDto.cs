namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents one known content usage of a media asset.
/// </summary>
public record MediaAssetUsageDto
{
    /// <summary>
    /// Usage domain identifier, for example field-report.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Stable domain-local record key.
    /// </summary>
    public string DomainKey { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable usage label.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Command-center or public route to the using record.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
