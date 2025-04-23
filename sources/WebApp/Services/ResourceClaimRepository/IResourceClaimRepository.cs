using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.UnitedTacticalForces.WebApp.Services.UserRepository;

namespace X39.UnitedTacticalForces.WebApp.Services.ResourceClaimRepository;

public interface IResourceClaimRepository
{
    Task<UserAndRoleTuple> GetResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        CancellationToken cancellationToken = default
    );

    Task AddUserResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        Guid userId,
        string resourceClaim,
        CancellationToken cancellationToken = default
    );

    Task RemoveUserResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        Guid userId,
        string resourceClaim,
        CancellationToken cancellationToken = default
    );

    Task DeleteClaimAsync(long claimId, CancellationToken cancellationToken = default);

    Task AddRoleResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        long roleId,
        string resourceClaim,
        bool set,
        CancellationToken cancellationToken = default
    );

    Task RemoveRoleResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        long roleId,
        string resourceClaim,
        CancellationToken cancellationToken = default
    );
}
