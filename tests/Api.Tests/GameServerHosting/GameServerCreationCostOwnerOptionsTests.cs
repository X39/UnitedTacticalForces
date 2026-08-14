using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;
using Claim = System.Security.Claims.Claim;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerCreationCostOwnerOptionsTests
{
    [Fact]
    public async Task CreatorOnlyReceivesPersonalAndExplicitlyUsableOwners()
    {
        var userId = Guid.NewGuid();
        var ownOwnerId = Guid.NewGuid();
        var grantedOwnerId = Guid.NewGuid();
        var hiddenOwnerId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        await using var store = await HostingTestStore.CreateAsync();
        store.DbContext.Users.Add(new User { PrimaryKey = userId, Nickname = "Creator" });
        store.DbContext.CostOwners.AddRange(
            Owner(ownOwnerId, "Personal", userId),
            Owner(grantedOwnerId, "Shared", null),
            Owner(hiddenOwnerId, "Hidden", null));
        await store.DbContext.SaveChangesAsync();

        var principal = Principal(
            userId,
            new Claim(Claims.ResourceBased.CostOwner.Use, grantedOwnerId.ToString()),
            new Claim(Claims.ResourceBased.CostOwner.Use, hiddenOwnerId.ToString().ToUpperInvariant()),
            new Claim(Claims.ResourceBased.CostOwner.Use, "not-a-guid"));
        var service = CreateService(store.DbContext);

        var options = await service.GetCreationCostOwnerOptionsAsync(principal);

        Assert.Equal([ownOwnerId, grantedOwnerId], options.Select(q => q.CostOwnerId));
        Assert.True(options.First().IsCurrentUser);
        Assert.DoesNotContain(options, q => q.CostOwnerId == hiddenOwnerId);
    }

    [Fact]
    public async Task HostingAdministratorReceivesEveryOwnerAndFrozenState()
    {
        var userId = Guid.NewGuid();
        var ownOwnerId = Guid.NewGuid();
        var frozenOwnerId = Guid.NewGuid();
        await using var store = await HostingTestStore.CreateAsync();
        store.DbContext.Users.Add(new User { PrimaryKey = userId, Nickname = "Administrator" });
        store.DbContext.CostOwners.AddRange(
            Owner(ownOwnerId, "Personal", userId),
            Owner(frozenOwnerId, "Verein", null, true));
        await store.DbContext.SaveChangesAsync();

        var principal = Principal(userId, new Claim(Claims.Administrative.Hosting, string.Empty));
        var options = await CreateService(store.DbContext).GetCreationCostOwnerOptionsAsync(principal);

        Assert.Equal(2, options.Count);
        Assert.True(options.Single(q => q.CostOwnerId == frozenOwnerId).IsFrozen);
    }

    private static GameServerProjectionService CreateService(DatabaseContext dbContext)
        => new(
            dbContext,
            new EmptyGameServerDefinitionCatalog(),
            [],
            null!,
            null!,
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            Options.Create(new StorageDemandOptions()),
            NullLogger<GameServerProjectionService>.Instance);

    private static CostOwner Owner(Guid id, string displayName, Guid? userId, bool frozen = false)
        => new()
        {
            PrimaryKey = id,
            DisplayName = displayName,
            UserFk = userId,
            Kind = userId.HasValue ? ECostOwnerKind.User : ECostOwnerKind.ExternalOrganization,
            IsFrozen = frozen,
            Currency = "EUR",
        };

    private static ClaimsPrincipal Principal(Guid userId, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new Claim(ClaimTypes.Name, userId.ToString()));
        identity.AddClaims(claims);
        return new ClaimsPrincipal(identity);
    }

    private sealed class EmptyGameServerDefinitionCatalog : IGameServerDefinitionCatalog
    {
        public IReadOnlyList<GameServerDefinition> GetDefinitions() => [];
    }
}
