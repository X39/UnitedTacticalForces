namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a simplified Data Transfer Object (DTO) for a user role.
/// </summary>
/// <remarks>
/// This class provides essential information about a role, including its unique identifier,
/// title, and description. It excludes detailed claims information.
/// </remarks>
public record PlainRoleDto
{
    /// <summary>
    /// Represents the unique identifier for this entity.
    /// This property is used to differentiate each instance of the associated record or object from others within the system.
    /// Depending on the context, it might correspond to a value in the database or serve as a reference for operations such as updates or deletions.
    /// </summary>
    public long PrimaryKey { get; set; }

    /// <summary>
    /// The Human-Readable title of this claim.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The description of this Role.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
