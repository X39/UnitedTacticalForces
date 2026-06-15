namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Direct server-scoped permissions assigned to one role.
/// </summary>
public sealed record GameServerRoleAccessGrantDto
{
    /// <summary>
    /// Role identity.
    /// </summary>
    public long RoleId { get; init; }

    /// <summary>
    /// Current role title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Current role description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Exact direct permissions assigned for the server.
    /// </summary>
    public string[] Permissions { get; init; } = [];
}
