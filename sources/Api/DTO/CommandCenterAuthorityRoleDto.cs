namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a compact role row for Command Center authority administration.
/// </summary>
public record CommandCenterAuthorityRoleDto
{
    /// <summary>
    /// The role metadata.
    /// </summary>
    public PlainRoleDto Role { get; init; } = new();

    /// <summary>
    /// Claims assigned to the role.
    /// </summary>
    public PlainClaimDto[] Claims { get; init; } = [];

    /// <summary>
    /// Amount of members assigned to the role.
    /// </summary>
    public int MemberCount { get; init; }
}
