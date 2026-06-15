namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents the aggregate Command Center snapshot for roles and claims.
/// </summary>
public record CommandCenterAuthoritySnapshotDto
{
    /// <summary>
    /// Total amount of user records.
    /// </summary>
    public long MemberCount { get; init; }

    /// <summary>
    /// Total amount of role records.
    /// </summary>
    public long RoleCount { get; init; }

    /// <summary>
    /// Total amount of claim records.
    /// </summary>
    public long ClaimCount { get; init; }

    /// <summary>
    /// Matching role rows for the current page query.
    /// </summary>
    public CommandCenterAuthorityRoleDto[] Roles { get; init; } = [];

    /// <summary>
    /// Matching claim rows for the current page query.
    /// </summary>
    public CommandCenterAuthorityClaimDto[] Claims { get; init; } = [];
}
