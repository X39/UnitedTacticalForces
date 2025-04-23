namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a plain data transfer object for a mod pack revision.
/// </summary>
/// <remarks>
/// This class is used to transfer data related to a mod pack revision,
/// including creation details, content, and associated metadata.
/// </remarks>
public record PlainModPackRevisionDto
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// The timestamp when this revision was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; init; }

    /// <summary>
    /// The HTML content of this revision.
    /// </summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>
    /// The foreign key of the user who updated this revision.
    /// </summary>
    public Guid UpdatedByFk { get; init; }

    /// <summary>
    /// Whether this revision is active or not.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Foreign key of the owning definition of this revision.
    /// </summary>
    public long DefinitionFk { get; init; }
}
