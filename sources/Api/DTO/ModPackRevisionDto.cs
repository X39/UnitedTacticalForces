namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Data Transfer Object (DTO) for a mod pack revision in the system.
/// </summary>
/// <remarks>
/// This class encapsulates information about a specific revision of a mod pack, including its metadata,
/// definition properties, and user-related data. It is typically used to transfer mod pack revision data
/// between application layers or to the client.
/// </remarks>
public record ModPackRevisionDto
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long? DefinitionPrimaryKey { get; init; }

    /// <summary>
    /// The timestamp when this modpack was created.
    /// </summary>
    public DateTimeOffset? DefinitionTimeStampCreated { get; init; }

    /// <summary>
    /// The name of this modpack.
    /// </summary>
    public string? DefinitionTitle { get; init; } = string.Empty;

    /// <summary>
    /// The foreign key of the owner from this modpack.
    /// </summary>
    public Guid? DefinitionOwnerFk { get; init; }

    /// <summary>
    /// Whether this modpack is active or not.
    /// </summary>
    public bool? DefinitionIsActive { get; init; }

    /// <summary>
    /// Whether this modpack is a composition of multiple modpack revisions
    /// or not.
    /// </summary>
    public bool? DefinitionIsComposition { get; init; }

    /// <summary>
    /// The timestamp indicating when the mod pack was downloaded.
    /// </summary>
    public DateTimeOffset? MetaTimeStampDownloaded { get; set; }

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
}
