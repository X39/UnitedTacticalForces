namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a composite data structure that encapsulates users and roles associated with a specific context or resource.
/// </summary>
/// <remarks>
/// This record is used to return a set of user data and their corresponding roles, allowing clients to manage
/// or query permissions, claims, or assignments. It is typically employed in scenarios requiring combined views
/// of user and role associations.
/// </remarks>
public record UserAndRoleTuple
{
    /// Represents an array of plain user data transfer objects within a user-role tuple.
    /// This property provides access to the collection of users associated with a specific context.
    public PlainUserDto[] Users { get; init; } = [];

    /// <summary>
    /// Represents the collection of roles associated with users in the context of resource claims.
    /// </summary>
    /// <remarks>
    /// This property provides details about the roles linked to specific claims and is typically used
    /// for managing role-based access control in the application.
    /// </remarks>
    public RoleDto[] Roles { get; init; } = [];
    
}
