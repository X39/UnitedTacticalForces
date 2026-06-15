namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a public page with a selected revision.
/// </summary>
public record PublicPageDto
{
    /// <summary>
    /// Page primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// Stable URL slug.
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// Whether the page is deleted from active workflows.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Selected or latest revision.
    /// </summary>
    public PublicPageRevisionDto? Revision { get; init; }

    /// <summary>
    /// All revisions when requested by administrative endpoints.
    /// </summary>
    public PublicPageRevisionDto[] Revisions { get; init; } = [];

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }
}
