namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a compact claim row for Command Center authority administration.
/// </summary>
public record CommandCenterAuthorityClaimDto
{
    /// <summary>
    /// The claim metadata.
    /// </summary>
    public PlainClaimDto Claim { get; init; } = new();

    /// <summary>
    /// Amount of members with this claim assigned directly.
    /// </summary>
    public int DirectMemberCount { get; init; }

    /// <summary>
    /// Amount of roles that include this claim.
    /// </summary>
    public int RoleCount { get; init; }

    /// <summary>
    /// Titles of roles that include this claim.
    /// </summary>
    public string[] RoleTitles { get; init; } = [];
}
