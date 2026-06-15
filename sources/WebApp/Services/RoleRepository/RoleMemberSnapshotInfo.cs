using X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.RoleRepository;

public record RoleMemberSnapshotInfo
{
    public PlainRoleDto Role { get; init; } = new();

    public long AssignedMemberCount { get; init; }

    public long AvailableMemberCount { get; init; }

    public CommandCenterCommunityMemberInfo[] AssignedMembers { get; init; } = [];

    public CommandCenterCommunityMemberInfo[] AvailableMembers { get; init; } = [];
}
