namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a detailed definition of a mod pack, including its metadata, ownership, status, and revisions.
/// </summary>
public record FullModPackDefinitionDto
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
    /// The owner of this modpack.
    /// </summary>
    public PlainUserDto? Owner { get; init; }
    
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
    /// The revisions which are part of this modpack.
    /// </summary>
    public PlainModPackRevisionDto[] ModPackRevisions { get; init; } = [];

}
