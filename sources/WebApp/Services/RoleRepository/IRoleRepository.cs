namespace X39.UnitedTacticalForces.WebApp.Services.RoleRepository;

public interface IRoleRepository
{
    Task<RoleMemberSnapshotInfo> GetRoleMembersAsync(
        long roleId,
        int take = 160,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task CreateRoleAsync(string title, string description, CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(
        long roleId,
        string title,
        string description,
        CancellationToken cancellationToken = default
    );

    Task DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default);

    Task SetUserRoleAsync(
        Guid userId,
        long roleId,
        bool roleActive,
        CancellationToken cancellationToken = default
    );

    Task SetRoleClaimAsync(
        long roleId,
        long claimId,
        bool claimActive,
        CancellationToken cancellationToken = default
    );
}
