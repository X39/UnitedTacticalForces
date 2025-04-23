namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a plain data transfer object for a mod pack definition in the system.
/// </summary>
/// <remarks>
/// This DTO is used to transfer basic mod pack information, including metadata such as
/// creation time, ownership, title, and its active or compositional state. It functions
/// as a simplified representation of a mod pack definition for API communication purposes.
/// </remarks>
public record PlainModPackDefinitionDto
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
}
