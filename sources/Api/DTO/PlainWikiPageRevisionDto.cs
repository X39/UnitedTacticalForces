namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a simplified data transfer object for a wiki page revision.
/// </summary>
/// <remarks>
/// This class is used to provide information about a specific revision of a wiki page,
/// including its content, author, and metadata.
/// </remarks>
public record WikiPageRevisionDto
{
    /// <summary>
    /// Represents the unique identifier for the entity.
    /// </summary>
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Foreign key to Page.
    /// </summary>
    public Guid PageForeignKey { get; set; }

    /// <summary>
    /// The timestamp indicating when the entity was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// The markdown content of this revision.
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>
    /// The author of this revision.
    /// </summary>
    public PlainUserDto? Author { get; set; }

    /// <summary>
    /// A comment for this revision, left by the author.
    /// </summary>
    /// <remarks>
    /// This is similar to a commit message in git.
    /// </remarks>
    public string Comment { get; set; } = string.Empty;
}
