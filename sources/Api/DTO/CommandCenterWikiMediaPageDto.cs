namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a compact wiki page row for Command Center wiki and media administration.
/// </summary>
public record CommandCenterWikiMediaPageDto
{
    /// <summary>
    /// The unique identifier for the wiki page.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// The wiki page title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Whether the page is currently deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// When the page was created.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Amount of revisions for this page.
    /// </summary>
    public int RevisionCount { get; init; }

    /// <summary>
    /// Timestamp of the latest revision.
    /// </summary>
    public Instant? LatestRevisionTimeStamp { get; init; }

    /// <summary>
    /// Display name of the latest revision author.
    /// </summary>
    public string? LatestRevisionAuthorName { get; init; }

    /// <summary>
    /// Comment attached to the latest revision.
    /// </summary>
    public string? LatestRevisionComment { get; init; }
}
