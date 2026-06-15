namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents one revision of a Verein document.
/// </summary>
public record VereinDocumentRevisionDto
{
    /// <summary>
    /// Revision primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// Document foreign key.
    /// </summary>
    public long DocumentFk { get; init; }

    /// <summary>
    /// Document-local version number.
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
    /// Markdown body.
    /// </summary>
    public string Markdown { get; init; } = string.Empty;

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
