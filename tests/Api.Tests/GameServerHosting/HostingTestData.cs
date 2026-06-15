using System.Security.Claims;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

internal static class HostingTestData
{
    public const string GameIdentifier = "test-game";

    public static async Task<HostingTestSeed> SeedServerAsync(
        DatabaseContext dbContext,
        bool withActiveDeployment = false,
        EDeploymentStatus deploymentStatus = EDeploymentStatus.Running)
    {
        var now = Instant.FromUnixTimeSeconds(1_700_000_000);
        var user = new User
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = "hosting-operator",
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord = new DiscordUser
            {
                Username = "hosting-operator",
            },
            Steam = new SteamUser(),
        };
        var owner = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = ECostOwnerKind.User,
            User             = user,
            UserFk           = user.PrimaryKey,
            DisplayName      = "Hosting operator",
            Currency         = "EUR",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var server = new GameServer
        {
            PrimaryKey                = Guid.NewGuid(),
            Title                     = "Integration server",
            GameIdentifier            = GameIdentifier,
            RuntimeProviderIdentifier = TestRuntimeProvider.Identifier,
            CreatedBy                 = user,
            CreatedByFk               = user.PrimaryKey,
            CurrentCostOwner          = owner,
            CurrentCostOwnerFk        = owner.PrimaryKey,
            TimeStampCreated          = now,
            TimeStampUpdated          = now,
        };
        var assignment = new GameServerCostOwnerAssignment
        {
            PrimaryKey        = Guid.NewGuid(),
            GameServer        = server,
            GameServerFk      = server.PrimaryKey,
            CostOwner         = owner,
            CostOwnerFk       = owner.PrimaryKey,
            AssignedBy        = user,
            AssignedByFk      = user.PrimaryKey,
            TimeStampAssigned = now,
        };
        GameServerDeployment? deployment = null;
        if (withActiveDeployment)
        {
            deployment = new GameServerDeployment
            {
                PrimaryKey       = Guid.NewGuid(),
                GameServer       = server,
                GameServerFk     = server.PrimaryKey,
                CostOwner        = owner,
                CostOwnerFk      = owner.PrimaryKey,
                Status           = deploymentStatus,
                Stage            = deploymentStatus.ToString().ToLowerInvariant(),
                PossibleCost     = true,
                TimeStampCreated = now,
                TimeStampUpdated = now,
                TimeStampStarted = now,
            };
        }

        dbContext.AddRange(user, owner, server, assignment);
        if (deployment is not null)
            dbContext.GameServerDeployments.Add(deployment);
        await dbContext.SaveChangesAsync();
        return new HostingTestSeed(user, owner, server, deployment);
    }

    public static ClaimsPrincipal Principal(
        User user,
        params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new System.Security.Claims.Claim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        foreach (var (type, value) in claims)
            identity.AddClaim(new System.Security.Claims.Claim(type, value));
        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal ServerPrincipal(User user, GameServer server, params string[] permissions)
        => Principal(user, permissions.Select(q => (q, server.PrimaryKey.ToString())).ToArray());

    public static GameServerLifecycleService CreateLifecycleService(DatabaseContext dbContext)
        => new(
            dbContext,
            new TestGameServerDefinitionCatalog(),
            [new TestRuntimeProvider()],
            new HostingAuthorizationService(dbContext),
            new HostingAuditWriter());

    public static GameServerProjectionService CreateProjectionService(DatabaseContext dbContext)
        => new(
            dbContext,
            new TestGameServerDefinitionCatalog(),
            [new TestRuntimeProvider()],
            new HostingAuthorizationService(dbContext));
}