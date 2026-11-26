namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One role that can be given direct access to a game server.
/// </summary>
public sealed record GameServerRoleAccessCandidateDto
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
    /// Number of active members of the role, counting only members that are verified and neither deleted nor
    /// banned, which is the number of people a grant to the role reaches.
    /// </summary>
    public int MemberCount { get; init; }
}
