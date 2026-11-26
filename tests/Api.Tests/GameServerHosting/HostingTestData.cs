using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
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

    /// <summary>
    /// Inserts one charged provider-cost slice into the ledger of a deployment, like the slice charging service does.
    /// </summary>
    /// <param name="dbContext">The database context the entry is saved in.</param>
    /// <param name="deployment">The deployment the slice is charged to.</param>
    /// <param name="startsAt">The start of the slice.</param>
    /// <param name="endsAt">The end of the slice.</param>
    /// <param name="amount">The positive amount charged for the whole slice.</param>
    /// <param name="providerIdentifier">
    /// The provider identifier recorded on the ledger entry, or <see langword="null"/> for the test provider.
    /// </param>
    /// <returns>A task resolving to the saved ledger entry, whose amount is the negated charge.</returns>
    public static async Task<HostingCostLedgerEntry> AddCostSliceAsync(
        DatabaseContext dbContext,
        GameServerDeployment deployment,
        Instant startsAt,
        Instant endsAt,
        decimal amount,
        string? providerIdentifier = null)
    {
        var hours = (decimal) (endsAt - startsAt).TotalHours;
        var entry = new HostingCostLedgerEntry
        {
            PrimaryKey          = Guid.NewGuid(),
            CostOwnerFk         = deployment.CostOwnerFk,
            DeploymentFk        = deployment.PrimaryKey,
            Kind                = EHostingLedgerEntryKind.ProviderCostSlice,
            Amount              = -amount,
            BalanceAfter        = 0m,
            EstimatedHourlyRate = hours > 0m ? decimal.Round(amount / hours, 6) : null,
            Currency            = "EUR",
            ProviderIdentifier  = providerIdentifier ?? TestRuntimeProvider.Identifier,
            SliceStartsAt       = startsAt,
            SliceEndsAt         = endsAt,
            TimeStampCreated    = startsAt,
            TimeStampEffective  = startsAt,
        };
        dbContext.HostingCostLedgerEntries.Add(entry);
        await dbContext.SaveChangesAsync();
        return entry;
    }

    /// <summary>
    /// Creates or replaces the cost alert setting of a cost owner with the given channel flags and thresholds.
    /// </summary>
    /// <param name="dbContext">The database context the setting is saved in.</param>
    /// <param name="costOwnerId">The cost owner the setting belongs to.</param>
    /// <param name="discordEnabled">Whether Discord direct messages are enabled.</param>
    /// <param name="emailEnabled">Whether email is enabled.</param>
    /// <param name="amounts">The absolute threshold amounts; none stores an empty threshold list.</param>
    /// <returns>A task resolving to the saved setting with its thresholds.</returns>
    public static async Task<CostAlertSetting> SetCostAlertThresholdsAsync(
        DatabaseContext dbContext,
        Guid costOwnerId,
        bool discordEnabled,
        bool emailEnabled,
        params decimal[] amounts)
    {
        var existing = await dbContext.CostAlertSettings
            .Include(q => q.Thresholds)
            .SingleOrDefaultAsync(q => q.CostOwnerFk == costOwnerId);
        if (existing is not null)
        {
            dbContext.CostAlertSettings.Remove(existing);
            await dbContext.SaveChangesAsync();
        }

        var now = Instant.FromUnixTimeSeconds(1_700_000_000);
        var setting = new CostAlertSetting
        {
            PrimaryKey       = Guid.NewGuid(),
            CostOwnerFk      = costOwnerId,
            DiscordEnabled   = discordEnabled,
            EmailEnabled     = emailEnabled,
            Revision         = 1,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        foreach (var amount in amounts)
        {
            setting.Thresholds.Add(new CostAlertThreshold
            {
                PrimaryKey = Guid.NewGuid(),
                SettingFk  = setting.PrimaryKey,
                Amount     = amount,
            });
        }

        dbContext.CostAlertSettings.Add(setting);
        await dbContext.SaveChangesAsync();
        return setting;
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
            new TestStorageDemandService(),
            new HostingAuthorizationService(dbContext),
            new HostingAuditWriter(),
            new TestGameServerSecretService(),
            new GameServerConfigurationService(dbContext),
            new GameServerContentSnapshotResolver(dbContext),
            Options.Create(new HostingFinanceOptions()));

    public static GameServerProjectionService CreateProjectionService(
        DatabaseContext dbContext,
        WorkerProvisioningOptions? workerProvisioningOptions = null)
        => new(
            dbContext,
            new TestGameServerDefinitionCatalog(),
            [new TestRuntimeProvider()],
            new TestStorageDemandService(),
            new HostingAuthorizationService(dbContext),
            new AdministrativeSshAccess(Options.Create(workerProvisioningOptions ?? new WorkerProvisioningOptions())),
            Options.Create(new StorageDemandOptions()),
            NullLogger<GameServerProjectionService>.Instance,
            new GameServerConfigurationService(dbContext),
            Options.Create(new HostingFinanceOptions()),
            Api.Tests.ServerAutomation.ServerAutomationTestData.CreateProjector(dbContext));
}
