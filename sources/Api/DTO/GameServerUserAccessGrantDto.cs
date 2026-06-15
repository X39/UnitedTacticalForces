namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Direct server-scoped permissions assigned to one user.
/// </summary>
public sealed record GameServerUserAccessGrantDto
{
    /// <summary>
    /// User identity.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Current display name.
    /// </summary>
    public string Nickname { get; init; } = string.Empty;

    /// <summary>
    /// Exact direct permissions assigned for the server.
    /// </summary>
    public string[] Permissions { get; init; } = [];
}
