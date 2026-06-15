using System.Security.Claims;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IHostingAuthorizationService
{
    bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission);

    Task<bool> CanUseCostOwnerAsync(
        ClaimsPrincipal principal,
        Guid costOwnerId,
        CancellationToken cancellationToken = default);

    Task<bool> CanViewCostOwnerAsync(
        ClaimsPrincipal principal,
        Guid costOwnerId,
        CancellationToken cancellationToken = default);

    Task<ClaimsPrincipal?> RebuildPrincipalAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
