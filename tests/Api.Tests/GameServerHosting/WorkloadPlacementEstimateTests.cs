using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Checks authoritative placement estimates and their isolation from accepted deployment state.</summary>
public sealed class WorkloadPlacementEstimateTests
{
    /// <summary>Prices distinct planned hosts and volumes using the same rules as Start.</summary>
    /// <param name="sameHost">Whether the auxiliary workload shares the primary host.</param>
    /// <param name="externalStorage">Whether each host receives a fixed external volume.</param>
    /// <param name="fallbackRate">Whether the provider fallback supplies the compute rate.</param>
    /// <returns>A task completing after prices and absence of orchestration mutations are verified.</returns>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public async Task SavedPlacement_QuotesHostsWithoutAcceptingDeployment(
        bool sameHost, bool externalStorage, bool fallbackRate)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        seed.Server.EstimatedHourlyCostEur = 7m;
        seed.Deployment!.EstimatedHourlyCostEur = 9m;
        seed.Deployment.PlacementSnapshot = "accepted-snapshot";
        if (sameHost)
            AddPlacement(store, seed, Arma3ConfigurationImplementation.SameHostPlacement);
        if (externalStorage)
        {
            seed.Server.RuntimeStorageStrategy = ERuntimeStorageStrategy.FixedExternal;
            seed.Server.ExternalStorageSizeGiB = 10;
        }
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var service = CreateService(store, fallbackRate ? null : 0.10m, fallbackRate ? 0.30m : null);

        var demand = await service.GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed));

        var estimate = Assert.IsType<WorkloadPlacementEstimateDto>(Assert.IsType<StorageDemandDto>(demand).PlacementEstimate);
        var hosts = sameHost ? 1 : 2;
        var compute = (fallbackRate ? 0.30m : 0.10m) * hosts;
        var storage = (externalStorage ? 0.20m : 0m) * hosts;
        Assert.Equal(hosts, estimate.HostCount);
        Assert.Equal(compute, estimate.EstimatedComputeHourlyCostEur);
        Assert.Equal(storage, estimate.EstimatedStorageHourlyCostEur);
        Assert.Equal(compute + storage, estimate.EstimatedHourlyCostEur);
        Assert.Equal(10m * hosts + storage * 730m, estimate.EstimatedMonthlyCostEur);
        Assert.Empty(store.DbContext.ChangeTracker.Entries());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Empty(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
        Assert.Empty(await store.DbContext.DeploymentResources.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleWorkers.ToArrayAsync());
        var deployment = await store.DbContext.GameServerDeployments.AsNoTracking().SingleAsync();
        Assert.Equal(9m, deployment.EstimatedHourlyCostEur);
        Assert.Equal("accepted-snapshot", deployment.PlacementSnapshot);
        Assert.Equal(7m, (await store.DbContext.GameServers.AsNoTracking().SingleAsync()).EstimatedHourlyCostEur);
    }

    /// <summary>Rejects unavailable prices and invalid saved placement without hiding storage evidence.</summary>
    /// <param name="invalidPlacement">Whether the saved selection is invalid instead of the price being missing.</param>
    /// <returns>A task completing after unavailable estimates are verified.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailablePlacement_ReturnsNullInsteadOfZero(bool invalidPlacement)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        if (invalidPlacement)
            AddPlacement(store, seed, "invalid-host");
        await store.DbContext.SaveChangesAsync();

        var demand = await CreateService(store, invalidPlacement ? 0.10m : null)
            .GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed));

        Assert.NotNull(demand);
        Assert.Null(demand.PlacementEstimate);
        Assert.Single(demand.RuntimeProfiles);
        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
    }

    /// <summary>Recomputes the next-deployment estimate after a persisted placement edit.</summary>
    /// <returns>A task completing after both saved selections have been read.</returns>
    [Fact]
    public async Task SavedPlacementChange_RefreshesEstimate()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store, 0.10m);
        var separate = await service.GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed));
        AddPlacement(store, seed, Arma3ConfigurationImplementation.SameHostPlacement);
        await store.DbContext.SaveChangesAsync();
        var same = await service.GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed));

        Assert.Equal(0.20m, separate!.PlacementEstimate!.EstimatedHourlyCostEur);
        Assert.Equal(0.10m, same!.PlacementEstimate!.EstimatedHourlyCostEur);
    }

    /// <summary>Requires server view permission without requiring server creation rights.</summary>
    /// <returns>A task completing after the authorized and denied reads are verified.</returns>
    [Fact]
    public async Task Estimate_RequiresServerViewPermission()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store, 0.10m);

        Assert.Null(await service.GetStorageDemandAsync(seed.Server.PrimaryKey, HostingTestData.Principal(seed.User)));
        Assert.NotNull((await service.GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed)))!.PlacementEstimate);
    }

    /// <summary>Marks orchestration settings without classifying generated game-file entries as control-plane settings.</summary>
    /// <returns>A task completing after the existing typed configuration response is inspected.</returns>
    [Fact]
    public async Task Configuration_ExposesControlPlaneMetadata()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        await store.DbContext.SaveChangesAsync();
        var controller = new GameServerConfigurationController(
            store.DbContext,
            new GameServerConfigurationService(store.DbContext),
            new HostingAuthorizationService(store.DbContext),
            new HostingAuditWriter())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Viewer(seed) },
            },
        };

        var result = Assert.IsType<OkObjectResult>(await controller.GetAsync(seed.Server.PrimaryKey, default));
        var configuration = Assert.IsType<GameConfigurationDto>(result.Value);
        var placement = Assert.Single(configuration.Entries, entry => entry.IsControlPlaneOnly);
        Assert.Equal(Arma3ConfigurationImplementation.HeadlessClientPlacementKey, placement.Key);
        Assert.Empty(placement.TargetPath);
        Assert.All(configuration.Entries.Where(entry => entry.TargetPath.Length > 0),
            entry => Assert.False(entry.IsControlPlaneOnly));
    }

    /// <summary>Leaves the ordinary single-host topology and optional monthly pricing generic.</summary>
    /// <returns>A task completing after a non-Arma implementation is estimated.</returns>
    [Fact]
    public async Task OtherImplementation_UsesDefaultTopologyAndOptionalMonthlyPrice()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = "aska";
        await store.DbContext.SaveChangesAsync();

        var demand = await CreateService(store, 0.10m, monthlyRate: null)
            .GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed));

        Assert.Equal(1, demand!.PlacementEstimate!.HostCount);
        Assert.Equal(0.10m, demand.PlacementEstimate.EstimatedHourlyCostEur);
        Assert.Null(demand.PlacementEstimate.EstimatedMonthlyCostEur);
    }

    /// <summary>Suppresses pricing when a saved allocation cannot be resolved from the current provider catalog.</summary>
    /// <param name="missingProfile">Whether the profile is absent instead of storage being invalid.</param>
    /// <returns>A task completing after the unavailable allocation is checked.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnavailableAllocation_PreservesStorageEvidence(bool missingProfile)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        if (missingProfile)
            seed.Server.RuntimeProfileIdentifier = "retired-profile";
        else
        {
            seed.Server.RuntimeStorageStrategy = ERuntimeStorageStrategy.FixedExternal;
            seed.Server.ExternalStorageSizeGiB = 1;
        }
        await store.DbContext.SaveChangesAsync();

        var demand = await CreateService(store, 0.10m).GetStorageDemandAsync(seed.Server.PrimaryKey, Viewer(seed));

        Assert.NotNull(demand);
        Assert.Null(demand.PlacementEstimate);
        Assert.Single(demand.RuntimeProfiles);
    }

    /// <summary>Creates a view-only principal with no creation or modification permission.</summary>
    /// <param name="seed">The actor and server to authorize.</param>
    /// <returns>A principal permitted to inspect this server.</returns>
    private static System.Security.Claims.ClaimsPrincipal Viewer(HostingTestSeed seed)
        => HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

    /// <summary>Creates a projection using real placement/configuration resolution and deterministic provider prices.</summary>
    /// <param name="store">The database supplying saved selections.</param>
    /// <param name="computeRate">The profile hourly rate, or null when unavailable.</param>
    /// <param name="fallbackRate">The configured provider fallback, if any.</param>
    /// <param name="monthlyRate">The optional provider monthly compute estimate.</param>
    /// <returns>The projection under test; provider mutation operations throw if called.</returns>
    private static GameServerProjectionService CreateService(
        HostingTestStore store, decimal? computeRate, decimal? fallbackRate = null, decimal? monthlyRate = 10m)
        => new(
            store.DbContext,
            new GameServerDefinitionCatalog(
                [new Arma3GameServerDefinitionRegistration(), new AskaGameServerDefinitionRegistration()]),
            [new TestRuntimeProvider
            {
                Profiles = [new RuntimeProfile("default", "Default", "Test profile.", DiskGiB: 80,
                    EstimatedHourlyCostEur: computeRate, EstimatedMonthlyCostEur: monthlyRate)],
                Capabilities = new RuntimeProviderCapabilities(true, true, true,
                    Storage: new RuntimeStorageCapabilities(true, 10, 100, 1, 0.02m)),
            }],
            new TestStorageDemandService(),
            new HostingAuthorizationService(store.DbContext),
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            Options.Create(new StorageDemandOptions()),
            NullLogger<GameServerProjectionService>.Instance,
            new GameServerConfigurationService(store.DbContext),
            Options.Create(new HostingFinanceOptions
            {
                EstimatedHourlyCostEur = fallbackRate.HasValue
                    ? new Dictionary<string, decimal> { [TestRuntimeProvider.Identifier] = fallbackRate.Value }
                    : [],
            }),
            Api.Tests.ServerAutomation.ServerAutomationTestData.CreateProjector(store.DbContext));

    /// <summary>Persists a placement override for subsequent effective-configuration reads.</summary>
    /// <param name="store">The context tracking the override.</param>
    /// <param name="seed">The owning server and editing actor.</param>
    /// <param name="value">The typed selection to serialize.</param>
    private static void AddPlacement(HostingTestStore store, HostingTestSeed seed, string value)
        => store.DbContext.GameServerConfigurationValues.Add(new GameServerConfigurationValue
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DefinitionKey = Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
            ValueJson = JsonSerializer.Serialize(value),
            UpdatedByFk = seed.User.PrimaryKey,
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        });
}
