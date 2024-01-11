using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ResourceClaimRepository;

[Scoped<ResourceClaimRepository, RepositoryBase>]
public sealed class ResourceClaimRepository : RepositoryBase, IResourceClaimRepository
{
    public ResourceClaimRepository(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl) { }

    public async Task<UserAndRoleTuple> GetResourceClaimAsync(
        string            resourcePrefix,
        string            resourceIdentifier,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.ResourceClaimUsersAndRolesAsync(
            resourcePrefix,
            resourceIdentifier,
            cancellationToken
        );
        return result;
    }

    public async Task SetUserResourceClaimAsync(
        string            resourcePrefix,
        string            resourceIdentifier,
        Guid              userId,
        string            resourceClaim,
        bool              set,
        CancellationToken cancellationToken = default)
    {
        if (set)
            await Client.ResourceClaimUsersAddAsync(
                resourcePrefix,
                resourceIdentifier,
                userId,
                resourceClaim,
                cancellationToken
            );
        else
            await Client.ResourceClaimUsersRemoveAsync(
                resourcePrefix,
                resourceIdentifier,
                userId,
                resourceClaim,
                cancellationToken
            );
    }

    public async Task DeleteClaimAsync(long claimId, CancellationToken cancellationToken = default)
    {
        await Client.ResourceClaimClaimsRemoveAsync(claimId, cancellationToken);
    }

    public async Task SetRoleResourceClaimAsync(
        string            resourcePrefix,
        string            resourceIdentifier,
        long              roleId,
        string            resourceClaim,
        bool              set,
        CancellationToken cancellationToken = default)
    {
        if (set)
            await Client.ResourceClaimRolesAddAsync(
                resourcePrefix,
                resourceIdentifier,
                (int)roleId,
                resourceClaim,
                cancellationToken
            );
        else
            await Client.ResourceClaimRolesRemoveAsync(
                resourcePrefix,
                resourceIdentifier,
                (int)roleId,
                resourceClaim,
                cancellationToken
            );
    }
}
