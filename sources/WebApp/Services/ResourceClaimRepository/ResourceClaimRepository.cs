using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ResourceClaimRepository;

[Scoped<ResourceClaimRepository, RepositoryBase>]
public sealed class ResourceClaimRepository : RepositoryBase, IResourceClaimRepository
{
    public ResourceClaimRepository(HttpClient httpClient, BaseUrl baseUrl)
        : base(httpClient, baseUrl) { }

    public async Task<UserAndRoleTuple> GetResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.ResourceClaim[resourcePrefix][resourceIdentifier]
            .UsersAndRoles
            .GetAsync(cancellationToken: cancellationToken);
        return result ?? throw new NullReferenceException("ResourceClaim is null.");
    }

    public async Task AddUserResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        Guid userId,
        string resourceClaim,
        CancellationToken cancellationToken = default
    )
    {
        await Client.ResourceClaim[resourcePrefix][resourceIdentifier]
            .Users[userId]
            .Add[resourceClaim]
            .GetAsync(cancellationToken: cancellationToken);
    }

    public async Task RemoveUserResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        Guid userId,
        string resourceClaim,
        CancellationToken cancellationToken = default
    )
    {
        await Client.ResourceClaim[resourcePrefix][resourceIdentifier]
            .Users[userId]
            .Remove[resourceClaim]
            .GetAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteClaimAsync(long claimId, CancellationToken cancellationToken = default)
    {
        await Client.ResourceClaim
            .Claims[claimId]
            .Remove
            .GetAsync(cancellationToken: cancellationToken);
    }

    public async Task AddRoleResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        long roleId,
        string resourceClaim,
        bool set,
        CancellationToken cancellationToken = default
    )
    {
        await Client.ResourceClaim[resourcePrefix][resourceIdentifier]
            .Roles[(int) roleId]
            .Add[resourceClaim]
            .GetAsync(cancellationToken: cancellationToken);
    }

    public async Task RemoveRoleResourceClaimAsync(
        string resourcePrefix,
        string resourceIdentifier,
        long roleId,
        string resourceClaim,
        CancellationToken cancellationToken = default
    )
    {
        await Client.ResourceClaim[resourcePrefix][resourceIdentifier]
            .Roles[(int) roleId]
            .Remove[resourceClaim]
            .GetAsync(cancellationToken: cancellationToken);
    }
}
