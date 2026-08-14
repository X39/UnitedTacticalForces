namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents the members assigned to, and available for, one authority role.
/// </summary>
public record CommandCenterAuthorityRoleMembersDto
{
    /// <summary>
    /// The selected role.
    /// </summary>
    public PlainRoleDto Role { get; init; } = new();

    /// <summary>
    /// Total members assigned to the role after applying the optional search.
    /// </summary>
    public long AssignedMemberCount { get; init; }

    /// <summary>
    /// Total members not assigned to the role after applying the optional search.
    /// </summary>
    public long AvailableMemberCount { get; init; }

    /// <summary>
    /// Matching assigned members.
    /// </summary>
    public CommandCenterCommunityMemberDto[] AssignedMembers { get; init; } = [];

    /// <summary>
    /// Matching members available for assignment.
    /// </summary>
    public CommandCenterCommunityMemberDto[] AvailableMembers { get; init; } = [];
}
