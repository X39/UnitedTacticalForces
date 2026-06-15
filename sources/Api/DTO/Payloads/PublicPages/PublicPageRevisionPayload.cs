namespace X39.UnitedTacticalForces.Api.DTO.Payloads.PublicPages;

/// <summary>
/// Payload for creating a public page revision.
/// </summary>
public record PublicPageRevisionPayload
{
    /// <summary>
    /// Display title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Short summary.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Small label rendered above the hero title.
    /// </summary>
    public string HeroKicker { get; init; } = string.Empty;

    /// <summary>
    /// Hero title.
    /// </summary>
    public string HeroTitle { get; init; } = string.Empty;

    /// <summary>
    /// Hero description.
    /// </summary>
    public string HeroDescription { get; init; } = string.Empty;

    /// <summary>
    /// Gallery media asset used as hero background image.
    /// </summary>
    public Guid HeroImageAssetFk { get; init; }

    /// <summary>
    /// Markdown body.
    /// </summary>
    public string Markdown { get; init; } = string.Empty;

    /// <summary>
    /// Optional structured content payload used by template-backed public pages.
    /// </summary>
    public string ContentJson { get; init; } = string.Empty;

    /// <summary>
    /// Revision comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Whether the revision should be published immediately.
    /// </summary>
    public bool IsPublished { get; init; }
}

