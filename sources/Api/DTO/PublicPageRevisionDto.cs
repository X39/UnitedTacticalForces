namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents one revision of a public page.
/// </summary>
public record PublicPageRevisionDto
{
    /// <summary>
    /// Revision primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// Page foreign key.
    /// </summary>
    public long PageFk { get; init; }

    /// <summary>
    /// Page-local version number.
    /// </summary>
    public int Version { get; init; }

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
    /// Computed API-relative hero image render path.
    /// </summary>
    public string HeroImagePath { get; init; } = string.Empty;

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
    public string Comment { get; init; } = string.Empty;

    /// <summary>
    /// Whether this revision is public.
    /// </summary>
    public bool IsPublished { get; init; }

    /// <summary>
    /// Publication timestamp.
    /// </summary>
    public Instant? TimeStampPublished { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Author user id.
    /// </summary>
    public Guid? AuthorFk { get; init; }

    /// <summary>
    /// Author display name.
    /// </summary>
    public string? AuthorName { get; init; }
}
