using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Verifies that Start persists workload placement and quotes its actual host allocations.</summary>
public sealed class WorkloadPlacementEnqueueTests
{
    /// <summary>Verifies that placement determines the estimate and stays fixed after the setting changes.</summary>
    /// <param name="sameHost">Whether to opt in to sharing the primary host.</param>
    /// <param name="externalStorage">Whether each allocated host receives an external volume.</param>
    /// <param name="fallbackRate">Whether compute must be priced with the configured provider fallback.</param>
    /// <returns>A task that completes once the persisted placement and quote have been checked.</returns>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task Start_SavesHostPlacementAndAggregateQuote(bool sameHost, bool externalStorage, bool fallbackRate)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        if (externalStorage)
        {
            seed.Server.RuntimeStorageStrategy = ERuntimeStorageStrategy.FixedExternal;
            seed.Server.ExternalStorageSizeGiB = 10;
        }

        var placement = sameHost
            ? AddPlacementOverride(store, seed, Arma3ConfigurationImplementation.SameHostPlacement)
            : null;
        await store.DbContext.SaveChangesAsync();

        var service = CreateArmaService(store, fallbackRate);
        var job = await service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            null,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Operate));

        if (placement is null)
            AddPlacementOverride(store, seed, Arma3ConfigurationImplementation.SameHostPlacement);
        else
            placement.ValueJson = JsonSerializer.Serialize(Arma3ConfigurationImplementation.SeparateHostPlacement);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var deployment = await store.DbContext.GameServerDeployments.SingleAsync(q => q.PrimaryKey == job.DeploymentFk);
        var snapshot = WorkloadPlacementPlan.Deserialize(Assert.IsType<string>(deployment.PlacementSnapshot));
        var primary = new WorkloadPlacementKey(GameServerNodeDefinition.PrimaryServiceIdentifier, 0);
        var headless = new WorkloadPlacementKey(Arma3GameServerDefinitionRegistration.HeadlessClientServiceIdentifier, 0);
        var hostCount = sameHost ? 1 : 2;
        var computePerHost = fallbackRate ? 0.30m : 0.10m;
        var storagePerHost = externalStorage ? 0.20m : 0m;

        Assert.Equal(primary, snapshot.PrimaryHostKey);
        Assert.Equal(hostCount, snapshot.Hosts.Count);
        Assert.Equal(2, snapshot.Workloads.Count);
        Assert.Contains(new WorkloadPlacementAssignment(primary, primary), snapshot.Workloads);
        Assert.Contains(new WorkloadPlacementAssignment(headless, sameHost ? primary : headless), snapshot.Workloads);
        Assert.All(snapshot.Hosts, host => Assert.Equal(storagePerHost, host.StoragePlan.EstimatedHourlyCostEur));
        Assert.Equal(computePerHost * hostCount, deployment.EstimatedComputeHourlyCostEur);
        Assert.Equal(storagePerHost * hostCount, deployment.EstimatedStorageHourlyCostEur);
        Assert.Equal((computePerHost + storagePerHost) * hostCount, deployment.EstimatedHourlyCostEur);
        Assert.Equal(deployment.EstimatedHourlyCostEur, snapshot.EstimatedHourlyCostEur);
    }

    /// <summary>Verifies that invalid legacy placement input cannot enqueue a billable deployment.</summary>
    /// <returns>A task that completes once Start rejection and its durable state have been checked.</returns>
    [Fact]
    public async Task Start_RejectsInvalidEffectivePlacement()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        AddPlacementOverride(store, seed, "unknown-host");
        await store.DbContext.SaveChangesAsync();

        var service = CreateArmaService(store, fallbackRate: false);
        await Assert.ThrowsAsync<HostingConflictException>(() => service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            null,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Operate)));

        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>Verifies that the existing default topology still produces one host for another implementation.</summary>
    /// <returns>A task that completes once the default plan has been checked.</returns>
    [Fact]
    public async Task Start_PreservesDefaultSingleHostForOtherImplementations()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var job = await service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            null,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Operate));

        var deployment = await store.DbContext.GameServerDeployments.SingleAsync(q => q.PrimaryKey == job.DeploymentFk);
        var snapshot = WorkloadPlacementPlan.Deserialize(Assert.IsType<string>(deployment.PlacementSnapshot));
        Assert.Equal(Assert.Single(snapshot.Hosts).Key, Assert.Single(snapshot.Workloads).HostKey);
        Assert.Equal(0.10m, deployment.EstimatedHourlyCostEur);
    }

    /// <summary>Creates the enqueue service with known profile and volume rates.</summary>
    /// <param name="store">The store supplying lifecycle state.</param>
    /// <param name="fallbackRate">Whether the compute profile omits its hourly rate.</param>
    /// <returns>The lifecycle service using Arma's actual placement definition.</returns>
    private static GameServerLifecycleService CreateArmaService(HostingTestStore store, bool fallbackRate)
        => new(
            store.DbContext,
            new GameServerDefinitionCatalog([new Arma3GameServerDefinitionRegistration()]),
            [
                new TestRuntimeProvider
                {
                    Profiles =
                    [
                        new RuntimeProfile(
                            "default",
                            "Default",
                            "Test compute profile.",
                            DiskGiB: 80,
                            EstimatedHourlyCostEur: fallbackRate ? null : 0.10m,
                            EstimatedMonthlyCostEur: 10m),
                    ],
                    Capabilities = new RuntimeProviderCapabilities(
                        true,
                        true,
                        true,
                        Storage: new RuntimeStorageCapabilities(true, 10, 100, 1, 0.02m)),
                },
            ],
            new TestStorageDemandService(),
            new HostingAuthorizationService(store.DbContext),
            new HostingAuditWriter(),
            new TestGameServerSecretService(),
            new GameServerConfigurationService(store.DbContext),
            Options.Create(new HostingFinanceOptions
            {
                EstimatedHourlyCostEur = new Dictionary<string, decimal> { [TestRuntimeProvider.Identifier] = 0.30m },
            }));

    /// <summary>Tracks a persisted server override for the placement setting.</summary>
    /// <param name="store">The store tracking changes for the test.</param>
    /// <param name="seed">The server and actor creating the override.</param>
    /// <param name="value">The selection to persist.</param>
    /// <returns>The tracked override, which may be changed after Start is accepted.</returns>
    private static GameServerConfigurationValue AddPlacementOverride(HostingTestStore store, HostingTestSeed seed,
        string value)
    {
        var placement = new GameServerConfigurationValue
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DefinitionKey = Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
            ValueJson = JsonSerializer.Serialize(value),
            UpdatedByFk = seed.User.PrimaryKey,
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        store.DbContext.Add(placement);
        return placement;
    }
}
