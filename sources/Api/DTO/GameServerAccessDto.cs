namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Direct user and role grants for one game server.
/// </summary>
public sealed record GameServerAccessDto
{
    /// <summary>
    /// Exact server-scoped permissions which may be delegated through this surface.
    /// </summary>
    public string[] AllowedPermissions { get; init; } = [];

    /// <summary>
    /// Users holding direct grants for this server.
    /// </summary>
    public GameServerUserAccessGrantDto[] Users { get; init; } = [];

    /// <summary>
    /// Roles holding direct grants for this server.
    /// </summary>
    public GameServerRoleAccessGrantDto[] Roles { get; init; } = [];
}
