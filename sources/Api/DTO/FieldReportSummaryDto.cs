namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a compact public field report row.
/// </summary>
public record FieldReportSummaryDto
{
    /// <summary>
    /// The unique report identifier.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// URL-safe public identifier.
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// Display title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Short public summary.
    /// </summary>
    public string Excerpt { get; init; } = string.Empty;

    /// <summary>
    /// Editorial category label.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Optional gallery media asset used as hero image.
    /// </summary>
    public Guid? HeroImageAssetFk { get; init; }

    /// <summary>
    /// Computed API-relative image render path.
    /// </summary>
    public string? HeroImagePath { get; init; }

    /// <summary>
    /// Alternative text for the hero image.
    /// </summary>
    public string? HeroImageAlt { get; init; }

    /// <summary>
    /// Whether this report is promoted in public feature slots.
    /// </summary>
    public bool IsFeatured { get; init; }

    /// <summary>
    /// Manual ordering value for public lists.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Publication timestamp.
    /// </summary>
    public Instant? TimeStampPublished { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }
}
