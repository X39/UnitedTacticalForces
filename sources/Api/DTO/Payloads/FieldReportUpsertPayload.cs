namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Payload used to create or update a field report.
/// </summary>
public record FieldReportUpsertPayload
{
    /// <summary>
    /// Optional URL-safe public identifier. Generated from title when omitted.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// Display title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Short public summary.
    /// </summary>
    public string? Excerpt { get; init; }

    /// <summary>
    /// Editorial category label.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Markdown body.
    /// </summary>
    public string? Markdown { get; init; }

    /// <summary>
    /// Optional gallery media asset used as hero image.
    /// </summary>
    public Guid? HeroImageAssetFk { get; init; }

    /// <summary>
    /// Alternative text for the hero image.
    /// </summary>
    public string? HeroImageAlt { get; init; }

    /// <summary>
    /// Whether this report should be promoted in public feature slots.
    /// </summary>
    public bool IsFeatured { get; init; }

    /// <summary>
    /// Manual ordering value for public lists.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Whether this report should be visible publicly.
    /// </summary>
    public bool IsPublished { get; init; }

    /// <summary>
    /// Publication timestamp. Defaults to current UTC time when publishing without a value.
    /// </summary>
    public Instant? TimeStampPublished { get; init; }
}
