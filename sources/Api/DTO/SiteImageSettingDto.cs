namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Public-site image slot backed by a reusable media asset.
/// </summary>
public record SiteImageSettingDto
{
    /// <summary>
    /// Stable setting key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable label.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Optional editor description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Editor grouping key.
    /// </summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// Display order within the editor group.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Current media asset id.
    /// </summary>
    public Guid ImageAssetFk { get; init; }

    /// <summary>
    /// API-relative image render path.
    /// </summary>
    public string ImagePath { get; init; } = string.Empty;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }

    /// <summary>
    /// Display name of the last updating user.
    /// </summary>
    public string? UpdatedByName { get; init; }
}
