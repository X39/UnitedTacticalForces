namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Data Transfer Object (DTO) for defining a mod pack within the United Tactical Forces API.
/// </summary>
/// <remarks>
/// This DTO contains properties that provide metadata and state information
/// about a mod pack, including creation timestamps, ownership details,
/// active state, and revision information.
/// </remarks>
public record ModPackDefinitionDto
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// The timestamp when this modpack was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; init; }

    /// <summary>
    /// The name of this modpack.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The foreign key of the owner from this modpack.
    /// </summary>
    public Guid OwnerFk { get; init; }

    /// <summary>
    /// Whether this modpack is active or not.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Whether this modpack is a composition of multiple modpack revisions
    /// or not.
    /// </summary>
    public bool IsComposition { get; init; }

    /// <summary>
    /// The timestamp indicating when the mod pack was downloaded.
    /// </summary>
    public DateTimeOffset? MetaTimeStampDownloaded { get; set; }

    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long? RevisionPrimaryKey { get; init; }

    /// <summary>
    /// The timestamp when this revision was created.
    /// </summary>
    public DateTimeOffset? RevisionTimeStampCreated { get; init; }

    /// <summary>
    /// The HTML content of this revision.
    /// </summary>
    public string? RevisionHtml { get; init; } = string.Empty;

    /// <summary>
    /// The foreign key of the user who updated this revision.
    /// </summary>
    public Guid? RevisionUpdatedByFk { get; init; }

    /// <summary>
    /// Whether this revision is active or not.
    /// </summary>
    public bool? RevisionIsActive { get; init; }
}
