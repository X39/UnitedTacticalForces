using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IGameServerAccessService
{
    IReadOnlyCollection<string> AllowedPermissions { get; }

    Task<GameServerAccessDto> GetAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task GrantUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task RevokeUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task GrantRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task RevokeRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
