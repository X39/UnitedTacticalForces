using X39.UnitedTacticalForces.WebApp.Services.UserRepository;

namespace X39.UnitedTacticalForces.WebApp.Services.ResourceClaimRepository;

public interface IResourceClaimRepository
{
    Task<UserAndRoleTuple> GetResourceClaimAsync(
        string            resourcePrefix,
        string            resourceIdentifier,
        CancellationToken cancellationToken = default);

    Task SetUserResourceClaimAsync(
        string            resourcePrefix,
        string            resourceIdentifier,
        Guid              userId,
        string            resourceClaim,
        bool              set,
        CancellationToken cancellationToken = default);
    
    Task DeleteClaimAsync(
        long             claimId,
        CancellationToken cancellationToken = default);

    Task SetRoleResourceClaimAsync(
        string            resourcePrefix,
        string            resourceIdentifier,
        long               roleId,
        string            resourceClaim,
        bool              set,
        CancellationToken cancellationToken = default);
}