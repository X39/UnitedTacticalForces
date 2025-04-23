namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a data transfer object for a wiki page.
/// </summary>
/// <remarks>
/// This class provides information about a wiki page, including its unique identifier, title, creation timestamp,
/// associated revisions, and its deletion status.
/// </remarks>
public record WikiPageDto
{
    /// <summary>
    /// The unique identifier for the object.
    /// </summary>
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// The title of the page.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp indicating when the wiki page was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// The content of the page and its revisions.
    /// The latest revision is always the one with the highest TimeStampCreated.
    /// </summary>
    public WikiPageRevisionDto[]? Revisions { get; set; }

    /// <summary>
    /// Whether this page is deleted.
    /// </summary>
    /// <remarks>
    /// Deleted pages are left in the database for archival purposes.
    /// </remarks>
    public bool IsDeleted { get; set; }
}
