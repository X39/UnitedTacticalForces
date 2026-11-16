using System.Text.Json;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace Api.Tests.GameServerHosting;

public sealed class LifecycleJobExecutorCloudTests
{
    [Fact]
    public async Task WaitForCommand_WhenWorkerProtocolAdvancesDeployment_DoesNotWriteStalePollingState()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var deployment = seed.Deployment!;
        deployment.Status = EDeploymentStatus.WaitingForWorker;
        deployment.Stage = "waiting-for-worker";
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            Identity = "concurrent-report-worker",
            Status = EWorkerStatus.Online,
            TimeStampRegistered = now,
            LastHeartbeatAt = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJobFk = job.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            ActionIdentifier = GameServerActions.Start,
            Status = EWorkerCommandStatus.Running,
            Stage = "running",
            TimeStampCreated = now,
            TimeStampStarted = now,
        };
        store.DbContext.AddRange(worker, command);
        await store.DbContext.SaveChangesAsync();

        var concurrentOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(store.Connection)
            .Options;
        await using (var concurrentContext = new NodaTimeSqliteApiDbContext(concurrentOptions))
        {
            var concurrentDeployment = await concurrentContext.GameServerDeployments
                .SingleAsync(q => q.PrimaryKey == deployment.PrimaryKey);
            concurrentDeployment.Status = EDeploymentStatus.Preparing;
            concurrentDeployment.Stage = "running";
            concurrentDeployment.TimeStampUpdated = now + Duration.FromMilliseconds(1);
            await concurrentContext.SaveChangesAsync();
        }

        var executor = CreateExecutor(store, new ExecutorTestProvider(), new RecordingCostSliceService());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.WaitForCommandAsync(
            job,
            seed.Server,
            deployment,
            worker,
            command,
            registrationDeadline: null,
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Start_PassesGameDeclaredInboundNetworkRequirementsToProvider()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, job) = await SeedRunningStartAsync(store);
        var requirements = new RuntimeInboundNetworkRequirement[]
        {
            new(ERuntimeNetworkProtocol.Tcp, 31000, 31000, "Primary test traffic"),
            new(ERuntimeNetworkProtocol.Udp, 31010, 31012, "Secondary test traffic"),
        };
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(
            store,
            provider,
            new RecordingCostSliceService(),
            new TestGameServerDefinitionCatalog(requirements));

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        var provisionRequest = Assert.IsType<RuntimeProvisionRequest>(provider.LastProvisionRequest);
        Assert.Equal(requirements, provisionRequest.InboundNetworkRequirements);
    }

    /// <summary>Verifies the same balance can fund one planned host while a two-host quote fails before create.</summary>
    /// <param name="sameHost">Whether the Headless Client shares the primary host.</param>
    /// <returns>A task that completes after checking the provider and financial reservation.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_ReservesTheFrozenAggregateBeforeCreatingAnyHost(bool sameHost)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider();
        var definition = new Arma3GameServerDefinitionRegistration();
        var catalog = new GameServerDefinitionCatalog([definition]);
        var placement = await SaveArmaPlacementAsync(store, seed, definition, sameHost);
        seed.CostOwner.Balance = 0.04m;
        seed.CostOwner.Allowance = null;
        await store.DbContext.SaveChangesAsync();
        var costs = new HostingCostSliceService(
            store.DbContext, new HostingAuditWriter(),
            Options.Create(new HostingFinanceOptions { MinimumSafetyRangeEur = 0m }));
        var executor = CreateExecutor(store, provider, costs, catalog);

        await Assert.ThrowsAnyAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(sameHost ? 1 : 0, provider.ProvisionCalls);
        Assert.Equal(sameHost ? 0.10m : 0.20m, placement.EstimatedHourlyCostEur);
        Assert.Equal(placement.EstimatedHourlyCostEur, seed.Deployment!.EstimatedHourlyCostEur);
        if (!sameHost)
            Assert.Equal("provider-cost-unaffordable", seed.Deployment.FaultCode);
    }

    /// <summary>Verifies host allocation and commands use the accepted snapshot after configuration changes.</summary>
    /// <param name="sameHost">Whether the accepted plan assigns both workloads to one host.</param>
    /// <returns>A task that completes after the executor reaches the worker boundary.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_AllocatesOnlyFrozenHostsAfterPlacementSettingChanges(bool sameHost)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider { AllocateReadyResources = true };
        var definition = new Arma3GameServerDefinitionRegistration();
        var catalog = new GameServerDefinitionCatalog([definition]);
        var placement = await SaveArmaPlacementAsync(store, seed, definition, sameHost);
        var value = await store.DbContext.GameServerConfigurationValues.SingleAsync();
        value.ValueJson = JsonSerializer.Serialize(sameHost
            ? Arma3ConfigurationImplementation.SeparateHostPlacement
            : Arma3ConfigurationImplementation.SameHostPlacement);
        await store.DbContext.SaveChangesAsync();
        seed.CostOwner.Balance = 10m;
        await store.DbContext.SaveChangesAsync();
        var costs = new HostingCostSliceService(
            store.DbContext, new HostingAuditWriter(), Options.Create(new HostingFinanceOptions()));
        var executor = CreateExecutor(store, provider, costs, catalog,
            new LifecycleWorkerOptions { RegistrationGrace = TimeSpan.Zero });

        await Assert.ThrowsAnyAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        var charge = Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
        Assert.Equal(placement.EstimatedHourlyCostEur, charge.EstimatedHourlyRate);
        Assert.Equal(placement.Hosts.Select(q => q.Key), provider.ProvisionRequests.Select(
            q => new WorkloadPlacementKey(q.ServiceIdentifier, q.ServiceOrdinal)));
        Assert.Equal(placement.Hosts.Count, provider.ProvisionRequests.Select(q => q.WorkerId).Distinct().Count());
        Assert.Equal(placement.Hosts.Count, provider.ProvisionRequests.Select(q => q.ProvisioningIntentId).Distinct().Count());
        Assert.Equal(definition.Definition.InboundNetworkRequirements, provider.ProvisionRequests[0].InboundNetworkRequirements);
        if (!sameHost)
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<RuntimeInboundNetworkRequirement>>(
                provider.ProvisionRequests[1].InboundNetworkRequirements));
    }

    /// <summary>Verifies legacy starts retain separate placement after an operator opts in to same-host placement.</summary>
    /// <param name="committed">Whether financing was already authorized by the prior executor.</param>
    /// <returns>A task that completes after checking legacy host allocation and the deployment quote.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_LegacyArmaUsesHistoricalTopologyAndPreservesCommittedRate(bool committed)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider { AllocateReadyResources = true };
        var definition = new Arma3GameServerDefinitionRegistration();
        await SaveArmaPlacementAsync(store, seed, definition, sameHost: true);
        var deployment = seed.Deployment!;
        deployment.PlacementSnapshot = null;
        deployment.EstimatedHourlyCostEur = 0.14m;
        if (committed)
            deployment.CostAuthorizationExpiresAt = NodaSystemClock.Instance.GetCurrentInstant() + Duration.FromHours(1);
        await store.DbContext.SaveChangesAsync();
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService(),
            new GameServerDefinitionCatalog([definition]), new LifecycleWorkerOptions { RegistrationGrace = TimeSpan.Zero });

        await Assert.ThrowsAnyAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(2, provider.ProvisionCalls);
        Assert.Equal(Arma3GameServerDefinitionRegistration.HeadlessClientServiceIdentifier,
            provider.ProvisionRequests[1].ServiceIdentifier);
        Assert.Equal(committed ? 0.14m : 0.20m, deployment.EstimatedHourlyCostEur);
        if (committed)
            Assert.Null(deployment.PlacementSnapshot);
        else
            Assert.Equal(2, WorkloadPlacementPlan.Deserialize(Assert.IsType<string>(deployment.PlacementSnapshot)).Hosts.Count);
    }

    /// <summary>Verifies that an unknown auxiliary create remains attributable through late verified teardown.</summary>
    /// <returns>A task that completes after the late graph is released.</returns>
    [Fact]
    public async Task Start_WhenAuxiliaryCreateIsAmbiguous_ReleasesLateAllocationWithoutReposting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider { AllocateReadyResources = true, FailAuxiliaryCreate = true };
        var definition = new Arma3GameServerDefinitionRegistration();
        await SaveArmaPlacementAsync(store, seed, definition, sameHost: false);
        var executor = CreateExecutor(
            store, provider, new RecordingCostSliceService(), new GameServerDefinitionCatalog([definition]));

        await Assert.ThrowsAnyAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await store.DbContext.Entry(seed.Deployment!).ReloadAsync();
        Assert.Null(seed.Deployment!.TimeStampResolved);
        Assert.Equal(EDeploymentStatus.TerminatingDueToFault, seed.Deployment.Status);
        Assert.Equal(2, provider.ProvisionCalls);
        Assert.Equal(Arma3GameServerDefinitionRegistration.HeadlessClientServiceIdentifier,
            provider.ProvisionRequests[1].ServiceIdentifier);

        provider.ReturnCompleteReadyGraphAfterUnknown = true;
        await Assert.ThrowsAnyAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        await store.DbContext.Entry(seed.Deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, seed.Deployment.Status);
        Assert.Equal(2, provider.ProvisionCalls);
        Assert.Equal(6, provider.ReleaseCalls);
        Assert.All(await store.DbContext.DeploymentResources.ToArrayAsync(),
            resource => Assert.NotNull(resource.TimeStampReleased));
    }

    /// <summary>Persists an Arma placement and quote for a seeded running Start job.</summary>
    /// <param name="store">The database containing the seeded job.</param>
    /// <param name="seed">The server and its deployment.</param>
    /// <param name="definition">Arma's real game definition.</param>
    /// <param name="sameHost">Whether the effective configuration selects shared placement.</param>
    /// <returns>The placement captured before the executor runs.</returns>
    private static async Task<WorkloadPlacementPlan> SaveArmaPlacementAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        Arma3GameServerDefinitionRegistration definition,
        bool sameHost)
    {
        seed.Server.GameIdentifier = Arma3ConfigurationImplementation.Identifier;
        store.DbContext.GameServerConfigurationValues.Add(new GameServerConfigurationValue
        {
            GameServerFk = seed.Server.PrimaryKey,
            DefinitionKey = Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
            UpdatedByFk = seed.User.PrimaryKey,
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
            ValueJson = JsonSerializer.Serialize(sameHost
                ? Arma3ConfigurationImplementation.SameHostPlacement
                : Arma3ConfigurationImplementation.SeparateHostPlacement),
        });
        await store.DbContext.SaveChangesAsync();
        var configuration = await new GameServerConfigurationService(store.DbContext).ResolveAsync(seed.Server.PrimaryKey);
        var placement = WorkloadPlacementResolver.Resolve(
            definition.Definition,
            configuration,
            new RuntimeProfile("default", "Default", "Test compute profile", EstimatedHourlyCostEur: 0.1m),
            new RuntimeStoragePlan(ERuntimeStorageStrategy.IncludedDisk, 0, 0, "/srv/united-tactical-forces/runtime", 0));
        seed.Deployment!.PlacementSnapshot = placement.Serialize();
        seed.Deployment.EstimatedHourlyCostEur = placement.EstimatedHourlyCostEur;
        await store.DbContext.SaveChangesAsync();
        return placement;
    }

    [Fact]
    public async Task Start_WithEmptyInboundNetworkPlan_FailsBeforeProviderInteraction()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(
            store,
            provider,
            new RecordingCostSliceService(),
            new TestGameServerDefinitionCatalog([]));

        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(0, provider.ProvisionCalls);
        Assert.Equal(0, provider.DiscoveryCalls);
        Assert.Null(provider.LastProvisionRequest);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal("game-network-plan-invalid", deployment.FaultCode);
        Assert.False(deployment.PossibleCost);
    }

    [Fact]
    public async Task Start_WithInvalidWorkerBootstrap_TerminallyRejectsBeforeProviderMutation()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider();
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(
            store,
            provider,
            cost,
            bootstrapRenderer: new ThrowingBootstrapRenderer());

        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(0, provider.ProvisionCalls);
        Assert.Equal(0, cost.ReserveCalls);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Failed, deployment.Status);
        Assert.Equal("worker-bootstrap-invalid", deployment.FaultCode);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        var worker = await store.DbContext.LifecycleWorkers.SingleAsync();
        Assert.Null(worker.BootstrapCredentialHash);
    }

    [Fact]
    public async Task Start_AfterIssuedUnknownCreateWithEmptyDiscovery_RemainsUnresolvedWithoutReposting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider();
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(3, provider.DiscoveryCalls);
        Assert.Equal(0, provider.ReleaseCalls);
        Assert.Equal(1, cost.ReserveCalls);
        Assert.Equal(1, cost.CommitCalls);
        var deployment = seed.Deployment!;
        var resource = Assert.Single(store.DbContext.DeploymentResources);
        Assert.NotNull(resource.TimeStampProvisioningRequested);
        Assert.Null(resource.TimeStampProvisioningResolved);
        Assert.Null(resource.TimeStampReleased);
        Assert.True(resource.PossibleCost);
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.True(deployment.IsProvisioningBlocked);
        Assert.Equal(EDeploymentStatus.TerminatingDueToFault, deployment.Status);
        Assert.Equal("provider-create-not-observed", deployment.FaultCode);
        Assert.Null(deployment.TimeStampResolved);
        Assert.True(deployment.PossibleCost);
    }

    [Fact]
    public async Task Start_WhenProviderConfirmsNoMutationWasIssued_ClearsClaimForSafeRetry()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ProvisionAttemptDefinitivelyClosed = true,
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        var intent = Assert.Single(store.DbContext.DeploymentResources);
        Assert.Null(intent.TimeStampProvisioningRequested);
        Assert.Null(intent.TimeStampProvisioningResolved);
        Assert.Null(intent.TimeStampReleased);
        Assert.False(intent.PossibleCost);

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(2, provider.ProvisionCalls);
        Assert.Null(intent.TimeStampProvisioningRequested);
    }

    [Fact]
    public async Task Start_WhenLeaseTokenIsCancelledAfterClaim_StillSettlesProviderResult()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, job) = await SeedRunningStartAsync(store);
        using var leaseCancellation = new CancellationTokenSource();
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ProvisionAttemptDefinitivelyClosed = true,
            OnProvisionAsync = () =>
            {
                leaseCancellation.Cancel();
                return Task.CompletedTask;
            },
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await Assert.ThrowsAsync<LifecycleExecutionException>(() =>
            executor.ExecuteAsync(job, leaseCancellation.Token));

        Assert.Equal(1, provider.ProvisionCalls);
        var intent = Assert.Single(store.DbContext.DeploymentResources);
        Assert.Null(intent.TimeStampProvisioningRequested);
        Assert.Null(intent.TimeStampProvisioningResolved);
        Assert.Null(intent.TimeStampReleased);
        Assert.False(intent.PossibleCost);
    }

    [Fact]
    public async Task Start_WhenProviderReturnsAfterCallDeadline_GetsFreshResultSettlementWindow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ReturnPartialServerOnUnknown = true,
            ProvisionAttemptDefinitivelyClosed = true,
            IgnoreProvisionCancellation = true,
            OnProvisionAsync = () => Task.Delay(TimeSpan.FromMilliseconds(600)),
        };
        var executor = CreateExecutor(
            store,
            provider,
            new RecordingCostSliceService(),
            workerOptions: new LifecycleWorkerOptions
            {
                ProviderSettlementTimeout = TimeSpan.FromMilliseconds(500),
            });

        var failure = await Record.ExceptionAsync(() => executor.ExecuteAsync(job));

        Assert.NotNull(failure);
        var concreteServer = await store.DbContext.DeploymentResources.SingleAsync(q => q.Kind == "server");
        Assert.Equal("server:42", concreteServer.ProviderResourceIdentifier);
        Assert.Equal(1, provider.ReleaseCalls);
    }

    [Fact]
    public async Task Start_WhenCostCommitFailsAfterProviderResult_PersistsCostBearingInventoryAtomically()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ReturnPartialServerOnUnknown = true,
        };
        var cost = new RecordingCostSliceService
        {
            CommitFailure = new InvalidOperationException("simulated crash before reservation commit"),
        };
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(job));

        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        var concreteServer = await store.DbContext.DeploymentResources.SingleAsync(q => q.Kind == "server");
        Assert.Equal("server:42", concreteServer.ProviderResourceIdentifier);
        Assert.True(concreteServer.PossibleCost);
        Assert.True(deployment.PossibleCost);
        Assert.Equal(1, cost.CommitCalls);
    }

    [Fact]
    public async Task Start_WithOverlappingApiExecutors_ClaimsProviderMutationExactlyOnce()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"hosting-provisioning-race-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        await using var anchorConnection = new SqliteConnection(connectionString);
        await anchorConnection.OpenAsync();
        var anchorOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(anchorConnection)
            .Options;
        await using var anchorContext = new NodaTimeSqliteApiDbContext(anchorOptions);
        await anchorContext.Database.EnsureCreatedAsync();
        var seed = await HostingTestData.SeedServerAsync(
            anchorContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Provisioning);
        var deployment = seed.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Server.RuntimeProviderIdentifier = ExecutorTestProvider.Identifier;
        deployment.PossibleCost = false;
        deployment.TimeStampStarted = null;
        seed.User.Claims!.Add(new Claim
        {
            Identifier = Claims.ResourceBased.Server.Operate,
            Value = seed.Server.PrimaryKey.ToString(),
            Category = "Game servers",
            Title = Claims.ResourceBased.Server.Operate,
            Description = "Test operation permission.",
        });
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        anchorContext.LifecycleJobs.Add(job);
        await anchorContext.SaveChangesAsync();

        await using var connectionA = new SqliteConnection(connectionString);
        await using var connectionB = new SqliteConnection(connectionString);
        await connectionA.OpenAsync();
        await connectionB.OpenAsync();
        var optionsA = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connectionA)
            .Options;
        var optionsB = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connectionB)
            .Options;
        await using var contextA = new NodaTimeSqliteApiDbContext(optionsA);
        await using var contextB = new NodaTimeSqliteApiDbContext(optionsB);
        var bothObservedEmpty = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryArrivals = 0;
        var providerCreateCalls = 0;
        async Task CoordinateInitialDiscoveryAsync(int discoveryCall)
        {
            if (discoveryCall != 1)
                return;
            if (Interlocked.Increment(ref discoveryArrivals) == 2)
                bothObservedEmpty.TrySetResult();
            await bothObservedEmpty.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Task RecordProviderCreateAsync()
        {
            Interlocked.Increment(ref providerCreateCalls);
            return Task.CompletedTask;
        }
        var providerA = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.PermanentFailure,
            OnDiscoveryAsync = CoordinateInitialDiscoveryAsync,
            OnProvisionAsync = RecordProviderCreateAsync,
        };
        var providerB = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.PermanentFailure,
            OnDiscoveryAsync = CoordinateInitialDiscoveryAsync,
            OnProvisionAsync = RecordProviderCreateAsync,
        };
        var executorA = CreateExecutor(contextA, providerA, new RecordingCostSliceService());
        var executorB = CreateExecutor(contextB, providerB, new RecordingCostSliceService());
        var invocationA = new LifecycleJob { PrimaryKey = job.PrimaryKey, Attempt = job.Attempt };
        var invocationB = new LifecycleJob { PrimaryKey = job.PrimaryKey, Attempt = job.Attempt };

        var failures = await Task.WhenAll(
            CaptureExceptionAsync(() => executorA.ExecuteAsync(invocationA)),
            CaptureExceptionAsync(() => executorB.ExecuteAsync(invocationB)));

        Assert.All(failures, Assert.NotNull);
        Assert.Equal(1, providerCreateCalls);
        anchorContext.ChangeTracker.Clear();
        var persistedIntent = await anchorContext.DeploymentResources.SingleAsync();
        Assert.NotNull(persistedIntent.TimeStampProvisioningRequested);
    }

    [Fact]
    public async Task Start_WhenTerminateCommitsImmediatelyAfterProviderClaim_ClosesAttemptAndTerminates()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"hosting-claim-barrier-race-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        await using var anchorConnection = new SqliteConnection(connectionString);
        await anchorConnection.OpenAsync();
        var anchorOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(anchorConnection)
            .Options;
        await using var anchorContext = new NodaTimeSqliteApiDbContext(anchorOptions);
        await anchorContext.Database.EnsureCreatedAsync();
        var (_, startJob) = await SeedRunningStartAsync(anchorContext);

        var interceptor = new ProvisioningClaimCommittedInterceptor(async () =>
        {
            await using var barrierConnection = new SqliteConnection(connectionString);
            await barrierConnection.OpenAsync();
            var barrierOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
                .UseSqlite(barrierConnection)
                .Options;
            await using var barrierContext = new NodaTimeSqliteApiDbContext(barrierOptions);
            var deployment = await barrierContext.GameServerDeployments
                .SingleAsync(q => q.PrimaryKey == startJob.DeploymentFk);
            deployment.IsProvisioningBlocked = true;
            deployment.Status = EDeploymentStatus.Terminating;
            deployment.Stage = "termination-requested";
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            await barrierContext.SaveChangesAsync();
        });
        await using var executorConnection = new SqliteConnection(connectionString);
        await executorConnection.OpenAsync();
        var executorOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(executorConnection)
            .AddInterceptors(interceptor)
            .Options;
        await using var executorContext = new NodaTimeSqliteApiDbContext(executorOptions);
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ProvisionAttemptDefinitivelyClosed = true,
        };
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(executorContext, provider, cost);
        var invocation = new LifecycleJob { PrimaryKey = startJob.PrimaryKey, Attempt = startJob.Attempt };

        await Assert.ThrowsAsync<LifecycleSupersededException>(() => executor.ExecuteAsync(invocation));

        Assert.Equal(1, provider.ProvisionCalls);
        anchorContext.ChangeTracker.Clear();
        var placeholder = await anchorContext.DeploymentResources.SingleAsync();
        Assert.NotNull(placeholder.TimeStampProvisioningRequested);
        Assert.NotNull(placeholder.TimeStampProvisioningResolved);
        Assert.NotNull(placeholder.TimeStampReleased);
        Assert.False(placeholder.PossibleCost);

        executorContext.ChangeTracker.Clear();
        var persistedStart = await executorContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == startJob.PrimaryKey);
        persistedStart.Status = ELifecycleJobStatus.CancelledByCostStop;
        persistedStart.TimeStampCompleted = NodaSystemClock.Instance.GetCurrentInstant();
        var terminateJob = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = persistedStart.GameServerFk,
            DeploymentFk = persistedStart.DeploymentFk,
            RequestedByFk = persistedStart.RequestedByFk,
            Kind = ELifecycleJobKind.Terminate,
            ActionIdentifier = GameServerActions.Terminate,
            Sequence = 2,
            Priority = int.MaxValue,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = int.MaxValue,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        executorContext.LifecycleJobs.Add(terminateJob);
        await executorContext.SaveChangesAsync();

        await executor.ExecuteAsync(terminateJob);

        executorContext.ChangeTracker.Clear();
        var resolvedDeployment = await executorContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == startJob.DeploymentFk);
        Assert.Equal(EDeploymentStatus.Terminated, resolvedDeployment.Status);
        Assert.NotNull(resolvedDeployment.TimeStampResolved);
        Assert.False(resolvedDeployment.PossibleCost);
        Assert.Equal(0, provider.ReleaseCalls);
    }

    [Fact]
    public async Task Start_WhenTerminateCommitsInSeparateContextBeforeResultAdoption_PersistsConcreteIdentifier()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"hosting-result-adoption-race-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        await using var anchorConnection = new SqliteConnection(connectionString);
        await anchorConnection.OpenAsync();
        var anchorOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(anchorConnection)
            .Options;
        await using var anchorContext = new NodaTimeSqliteApiDbContext(anchorOptions);
        await anchorContext.Database.EnsureCreatedAsync();
        var (_, startJob) = await SeedRunningStartAsync(anchorContext);

        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ReturnPartialFirewallOnUnknown = true,
            ReturnPartialServerOnUnknown = true,
            ProvisionAttemptDefinitivelyClosed = true,
            OnProvisionAsync = async () =>
            {
                await using var barrierConnection = new SqliteConnection(connectionString);
                await barrierConnection.OpenAsync();
                var barrierOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
                    .UseSqlite(barrierConnection)
                    .Options;
                await using var barrierContext = new NodaTimeSqliteApiDbContext(barrierOptions);
                var deployment = await barrierContext.GameServerDeployments
                    .SingleAsync(q => q.PrimaryKey == startJob.DeploymentFk);
                deployment.IsProvisioningBlocked = true;
                deployment.Status = EDeploymentStatus.Terminating;
                deployment.Stage = "termination-requested";
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                var intent = await barrierContext.DeploymentResources
                    .SingleAsync(q => q.DeploymentFk == startJob.DeploymentFk);
                var observedAt = NodaSystemClock.Instance.GetCurrentInstant();
                barrierContext.DeploymentResources.Add(new DeploymentResource
                {
                    PrimaryKey = Guid.NewGuid(),
                    DeploymentFk = deployment.PrimaryKey,
                    ProviderIdentifier = ExecutorTestProvider.Identifier,
                    ProvisioningIntentIdentifier = intent.ProvisioningIntentIdentifier,
                    ProviderResourceIdentifier = "firewall:19",
                    Kind = "firewall",
                    State = EDeploymentResourceState.Allocated,
                    PossibleCost = false,
                    TimeStampCreated = observedAt,
                    TimeStampUpdated = observedAt,
                    TimeStampProvisioningRequested = observedAt,
                    TimeStampProvisioningResolved = observedAt,
                });
                await barrierContext.SaveChangesAsync();
            },
        };
        await using var executorConnection = new SqliteConnection(connectionString);
        await executorConnection.OpenAsync();
        var executorOptions = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(executorConnection)
            .Options;
        await using var executorContext = new NodaTimeSqliteApiDbContext(executorOptions);
        var executor = CreateExecutor(executorContext, provider, new RecordingCostSliceService());
        var invocation = new LifecycleJob { PrimaryKey = startJob.PrimaryKey, Attempt = startJob.Attempt };

        await Assert.ThrowsAsync<LifecycleSupersededException>(() => executor.ExecuteAsync(invocation));

        anchorContext.ChangeTracker.Clear();
        var resources = await anchorContext.DeploymentResources.ToArrayAsync();
        Assert.Equal(2, resources.Length);
        var concreteServer = resources.Single(q => q.Kind == "server");
        Assert.Equal("server:42", concreteServer.ProviderResourceIdentifier);
        Assert.NotNull(concreteServer.TimeStampProvisioningResolved);
        Assert.Null(concreteServer.TimeStampReleased);
        Assert.True(concreteServer.PossibleCost);
        Assert.Equal("firewall:19", resources.Single(q => q.Kind == "firewall").ProviderResourceIdentifier);
    }

    [Fact]
    public async Task Start_WhenProviderClosesKnownPartialGraph_FaultStopsAndFinalizesTeardown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ReturnPartialFirewallOnUnknown = true,
            ProvisionAttemptDefinitivelyClosed = true,
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.True(deployment.IsProvisioningBlocked);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(1, provider.ReleaseCalls);
        var resources = await store.DbContext.DeploymentResources.ToArrayAsync();
        Assert.Equal(2, resources.Length);
        Assert.All(resources, q => Assert.NotNull(q.TimeStampProvisioningResolved));
        Assert.All(resources, q => Assert.NotNull(q.TimeStampReleased));
        Assert.All(resources, q => Assert.False(q.PossibleCost));
    }

    [Fact]
    public async Task Start_WhenTerminateClosesBarrierAfterConcreteServerCreate_RequiresVerifiedTeardown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, startJob) = await SeedRunningStartAsync(store);
        var deployment = seed.Deployment!;
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.TransientFailure,
            ReturnPartialFirewallOnUnknown = true,
            ReturnPartialServerOnUnknown = true,
            ProvisionAttemptDefinitivelyClosed = true,
            OnProvisionAsync = async () =>
            {
                deployment.IsProvisioningBlocked = true;
                deployment.Status = EDeploymentStatus.Terminating;
                deployment.Stage = "termination-requested";
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                await store.DbContext.SaveChangesAsync();
            },
        };
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<LifecycleSupersededException>(() => executor.ExecuteAsync(startJob));

        var concreteServer = store.DbContext.DeploymentResources.Single(q => q.Kind == "server");
        Assert.Equal("server:42", concreteServer.ProviderResourceIdentifier);
        Assert.NotNull(concreteServer.TimeStampProvisioningResolved);
        Assert.Null(concreteServer.TimeStampReleased);
        Assert.True(concreteServer.PossibleCost);
        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(1, cost.CommitCalls);
        Assert.Equal(0, cost.ReleaseCalls);

        startJob.Status = ELifecycleJobStatus.CancelledByCostStop;
        startJob.TimeStampCompleted = NodaSystemClock.Instance.GetCurrentInstant();
        await store.DbContext.SaveChangesAsync();
        var terminateJob = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Terminate,
            ActionIdentifier = GameServerActions.Terminate,
            Sequence = 2,
            Priority = int.MaxValue,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = int.MaxValue,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        store.DbContext.LifecycleJobs.Add(terminateJob);
        await store.DbContext.SaveChangesAsync();

        await executor.ExecuteAsync(terminateJob);

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Terminated, deployment.Status);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Equal(2, provider.ReleaseCalls);
        Assert.All(store.DbContext.DeploymentResources, resource => Assert.NotNull(resource.TimeStampReleased));
    }

    [Fact]
    public async Task Start_AfterUnknownPartialGraph_WaitsForLatePrimaryThenTearsDownWithoutReposting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ReturnPartialFirewallOnUnknown = true,
        };
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(3, provider.DiscoveryCalls);
        Assert.Equal(1, provider.ReleaseCalls);
        Assert.Equal(1, cost.ReserveCalls);
        Assert.Equal(1, cost.CommitCalls);
        Assert.Equal(2, store.DbContext.DeploymentResources.Count());
        var placeholder = store.DbContext.DeploymentResources.Single(q => q.Kind == "server");
        Assert.Null(placeholder.ProviderResourceIdentifier);
        Assert.Null(placeholder.TimeStampProvisioningResolved);
        Assert.Null(placeholder.TimeStampReleased);
        Assert.True(placeholder.PossibleCost);
        var firewall = store.DbContext.DeploymentResources.Single(q => q.Kind == "firewall");
        Assert.NotNull(firewall.TimeStampReleased);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatingDueToFault, deployment.Status);
        Assert.Equal("provider-resource-graph-incomplete", deployment.FaultCode);
        Assert.True(deployment.PossibleCost);

        provider.ReturnCompleteReadyGraphAfterUnknown = true;
        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(5, provider.DiscoveryCalls);
        Assert.Equal(3, provider.ReleaseCalls);
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.False(deployment.PossibleCost);
        Assert.All(store.DbContext.DeploymentResources, q => Assert.NotNull(q.TimeStampReleased));
    }

    [Fact]
    public async Task Start_AfterUnknownCreateWithStableDuplicateGraph_FaultTearsDownEveryResourceWithoutReposting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ReturnDuplicateServersAfterUnknown = true,
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(2, provider.ReleaseCalls);
        Assert.All(store.DbContext.DeploymentResources, q => Assert.NotNull(q.TimeStampReleased));
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-resource-graph-duplicate", deployment.FaultCode);
        Assert.False(deployment.PossibleCost);
    }

    [Fact]
    public async Task Start_AfterUnknownCreateWithCompleteButPendingGraph_RetriesWithoutRepostingOrTeardown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ReturnCompletePendingGraphAfterUnknown = true,
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(0, provider.ReleaseCalls);
        Assert.Equal(2, provider.DiscoveryCalls);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Provisioning, deployment.Status);
        Assert.Equal("provider-resource-graph-readiness-pending", deployment.Stage);
        Assert.True(deployment.PossibleCost);
        var primaryIp = store.DbContext.DeploymentResources.Single(q => q.Kind == "primary-ip");
        Assert.Equal("allocated", primaryIp.ProviderState);
        Assert.Equal("203.0.113.91", primaryIp.PublicAddress);
    }

    [Fact]
    public async Task Start_AfterUnknownCreateWithCompleteConflictingGraph_FaultTearsDownWithoutReposting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ReturnCompleteConflictingGraphAfterUnknown = true,
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(3, provider.ReleaseCalls);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-resource-graph-safety-conflict", deployment.FaultCode);
        Assert.False(deployment.PossibleCost);
    }

    [Fact]
    public async Task Start_WhenSliceIsUnaffordable_FailsBeforeAnyProviderCreate()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider();
        var cost = new RecordingCostSliceService
        {
            Failure = new HostingAffordabilityException("insufficient test credit"),
        };
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(0, provider.ProvisionCalls);
        Assert.Equal(1, provider.DiscoveryCalls);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Failed, deployment.Status);
        Assert.Equal("provider-cost-unaffordable", deployment.FaultCode);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
    }

    [Fact]
    public async Task Start_WhenProviderRejectsWithoutAllocating_ReleasesUnchargedReservation()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.PermanentFailure,
        };
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(1, cost.ReserveCalls);
        Assert.Equal(0, cost.CommitCalls);
        Assert.Equal(1, cost.ReleaseCalls);
        var operationKinds = await store.DbContext.HostingAuditRecords
            .Select(q => q.Kind)
            .ToArrayAsync();
        Assert.Contains(EHostingAuditEventKind.DeploymentResourceProvisioningRequested, operationKinds);
        Assert.Contains(EHostingAuditEventKind.DeploymentResourceProvisioningFailed, operationKinds);
    }

    [Fact]
    public async Task Start_WhenPermanentPartialGraphIsNonBillable_ReleasesReservationBeforeCleanup()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider
        {
            ProvisionOutcome = ERuntimeProviderOutcome.PermanentFailure,
            ReturnPartialFirewallOnUnknown = true,
        };
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAnyAsync<Exception>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, cost.ReserveCalls);
        Assert.Equal(0, cost.CommitCalls);
        Assert.Equal(1, cost.ReleaseCalls);
    }

    [Fact]
    public async Task RuntimeCommand_PersistsStableDeduplicatedContentSnapshot()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Running);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        seed.Server.RuntimeProviderIdentifier = ExecutorTestProvider.Identifier;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var definition = new ModPackDefinition
        {
            Title = "Runtime selection",
            GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
            OwnerFk = seed.User.PrimaryKey,
            IsActive = true,
            TimeStampCreated = now,
            ModPackRevisionsOwned = [],
        };
        var mod = new Mod
        {
            PrimaryKey = Guid.NewGuid(),
            GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
            ProviderIdentifier = ModSourceProviderIdentifiers.SteamWorkshop,
            CanonicalSourceIdentifier = "450814997",
            Title = "CBA",
            UsageRole = EModUsageRole.Shared,
            IsActive = true,
            TimeStampCreated = now,
        };
        mod.Versions.Add(new ModVersion
        {
            PrimaryKey = Guid.NewGuid(),
            Mod = mod,
            ModFk = mod.PrimaryKey,
            VersionNumber = 1,
            VersionLabel = "Current Workshop version",
            VersionSelector = string.Empty,
            InstallerIdentifier = "arma3-workshop",
            TimeStampCreated = now,
        });
        var revision = new ModPackRevision
        {
            Definition = definition,
            UpdatedByFk = seed.User.PrimaryKey,
            IsActive = true,
            TimeStampCreated = now,
            Mods = [new ModPackRevisionMod { Mod = mod, ModFk = mod.PrimaryKey, Order = 0 }],
        };
        definition.ModPackRevisionsOwned.Add(revision);
        seed.Server.SelectedModPackDefinition = definition;
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment!.PrimaryKey,
            Identity = "content-snapshot-worker",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = now,
            TimeStampRegistered = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.CollectLogs,
            ActionIdentifier = GameServerActions.CollectLogs,
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.AddRange(definition, worker, job);
        await store.DbContext.SaveChangesAsync();
        var executor = CreateExecutor(
            store,
            new ExecutorTestProvider(),
            new RecordingCostSliceService());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(job, cancellation.Token));

        var command = await store.DbContext.WorkerCommands.SingleAsync();
        var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(
            command.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var content = Assert.Single(payload!.Content);
        Assert.Equal("steam-workshop", content.Source);
        Assert.Equal("450814997", content.Identifier);
        Assert.Equal("CBA", content.DisplayName);
    }

    /// <summary>Verifies that user termination does not depend on a readable placement snapshot.</summary>
    /// <param name="placementSnapshot">A missing or unsupported placement snapshot.</param>
    /// <returns>A task that completes after verified teardown.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("{\"version\":99}")]
    public async Task UserTerminate_SkipsWorkerPreservationAndPerformsVerifiedProviderTeardown(string? placementSnapshot)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job, _) = await SeedTerminateAsync(store, faultTriggered: false);
        seed.Deployment!.PlacementSnapshot = placementSnapshot;
        await store.DbContext.SaveChangesAsync();
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await executor.ExecuteAsync(job);

        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Terminated, deployment.Status);
        Assert.Equal(2, provider.DiscoveryCalls);
        Assert.Empty(store.DbContext.WorkerCommands);
    }

    [Theory]
    [InlineData(EDeploymentStatus.Terminated)]
    [InlineData(EDeploymentStatus.TerminatedDueToFault)]
    public async Task Terminate_WhenDeploymentWasAlreadyResolved_IsIdempotent(EDeploymentStatus terminalStatus)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job, _) = await SeedTerminateAsync(store, faultTriggered: false);
        var deployment = seed.Deployment!;
        var resolvedAt = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.Status = terminalStatus;
        deployment.Stage = terminalStatus == EDeploymentStatus.Terminated ? "terminated" : "terminated-due-to-fault";
        deployment.TimeStampResolved = resolvedAt;
        deployment.PossibleCost = false;
        await store.DbContext.SaveChangesAsync();
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await executor.ExecuteAsync(job);

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(terminalStatus, deployment.Status);
        Assert.Equal(resolvedAt, deployment.TimeStampResolved);
        Assert.Equal(0, provider.DiscoveryCalls);
        Assert.Equal(0, provider.ReleaseCalls);
    }

    [Fact]
    public async Task OrdinaryStop_WhenUserTerminateCommitsDuringFinalVerification_DefersTerminalPublication()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Running);
        var deployment = seed.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Server.RuntimeProviderIdentifier = ExecutorTestProvider.Identifier;
        deployment.IsProvisioningBlocked = true;
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            Identity = "stop-race-worker",
            WorkerCredentialHash = new string('a', 64),
            WorkerCredentialIssuedAt = now,
            WorkerCredentialExpiresAt = now + Duration.FromHours(1),
            WorkerCredentialActivatedAt = now,
            TimeStampRegistered = now,
            Status = EWorkerStatus.Online,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var stopJob = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Stop,
            ActionIdentifier = GameServerActions.Stop,
            Sequence = 1,
            Priority = 50,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var stopCommand = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJobFk = stopJob.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            Priority = 50,
            ActionIdentifier = GameServerActions.Stop,
            Status = EWorkerCommandStatus.Succeeded,
            Stage = "completed",
            TimeStampCreated = now,
            TimeStampStarted = now,
            TimeStampCompleted = now,
        };
        deployment.Workers.Add(worker);
        stopJob.WorkerCommands.Add(stopCommand);
        store.DbContext.AddRange(worker, stopJob, stopCommand);
        await store.DbContext.SaveChangesAsync();

        LifecycleJob? queuedTerminate = null;
        var provider = new ExecutorTestProvider
        {
            OnDiscoveryAsync = async discoveryCall =>
            {
                if (discoveryCall != 2)
                    return;
                queuedTerminate = new LifecycleJob
                {
                    PrimaryKey = Guid.NewGuid(),
                    GameServerFk = seed.Server.PrimaryKey,
                    DeploymentFk = deployment.PrimaryKey,
                    RequestedByFk = seed.User.PrimaryKey,
                    Kind = ELifecycleJobKind.Terminate,
                    ActionIdentifier = GameServerActions.Terminate,
                    Sequence = 2,
                    Priority = 100,
                    Status = ELifecycleJobStatus.Queued,
                    Stage = "queued",
                    MaxAttempts = int.MaxValue,
                    TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
                    TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
                };
                stopJob.Status = ELifecycleJobStatus.CancelledByCostStop;
                stopJob.Stage = "cancelled-by-cost-stop";
                deployment.Status = EDeploymentStatus.Terminating;
                deployment.Stage = "termination-requested";
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                store.DbContext.LifecycleJobs.Add(queuedTerminate);
                await store.DbContext.SaveChangesAsync();
            },
        };
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await executor.ExecuteAsync(stopJob);

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Terminating, deployment.Status);
        Assert.Equal("termination-requested", deployment.Stage);
        Assert.Null(deployment.TimeStampResolved);
        Assert.Equal(2, provider.DiscoveryCalls);
        Assert.Null(worker.WorkerCredentialRevokedAt);

        // The queued Terminate owns terminal publication even though the older Stop already removed provider cost.
        Assert.NotNull(queuedTerminate);
        queuedTerminate.Status = ELifecycleJobStatus.Running;
        queuedTerminate.Stage = "claimed";
        await store.DbContext.SaveChangesAsync();
        await executor.ExecuteAsync(queuedTerminate);
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Terminated, deployment.Status);
        Assert.Equal("terminated", deployment.Stage);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.NotNull(worker.WorkerCredentialRevokedAt);
        Assert.Equal(4, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task FaultTerminate_BoundsEvidenceWindowThenContinuesVerifiedProviderTeardown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job, _) = await SeedTerminateAsync(store, faultTriggered: true);
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(
            store,
            provider,
            new RecordingCostSliceService(),
            workerOptions: new LifecycleWorkerOptions
            {
                RegistrationGrace = TimeSpan.FromMinutes(15),
                BootstrapCredentialLifetime = TimeSpan.FromMinutes(45),
                IdlePollInterval = TimeSpan.FromMilliseconds(2),
                FaultEvidenceCollectionWindow = TimeSpan.FromMilliseconds(20),
            });

        await executor.ExecuteAsync(job);

        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal(2, provider.DiscoveryCalls);
        var command = Assert.Single(store.DbContext.WorkerCommands);
        Assert.Equal(GameServerActionIds.FaultStop, command.ActionIdentifier);
        Assert.Equal(EWorkerCommandStatus.CancelledByCostStop, command.Status);
        Assert.Equal("fault-evidence-window-expired", command.Stage);
        var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(
            command.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(
            (deployment.TimeStampFaulted!.Value + Duration.FromMilliseconds(20)).ToDateTimeOffset(),
            payload!.FaultEvidenceDeadline);
    }

    [Fact]
    public async Task FaultTerminate_DoesNotRestartTheEvidenceWindowAfterCoordinationDelay()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job, _) = await SeedTerminateAsync(store, faultTriggered: true);
        var faultedAt = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(2);
        seed.Deployment!.TimeStampFaulted = faultedAt;
        await store.DbContext.SaveChangesAsync();
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(
            store,
            provider,
            new RecordingCostSliceService(),
            workerOptions: new LifecycleWorkerOptions
            {
                RegistrationGrace = TimeSpan.FromMinutes(15),
                BootstrapCredentialLifetime = TimeSpan.FromMinutes(45),
                IdlePollInterval = TimeSpan.FromMilliseconds(2),
                FaultEvidenceCollectionWindow = TimeSpan.FromMinutes(1),
            });

        await executor.ExecuteAsync(job);

        var command = Assert.Single(store.DbContext.WorkerCommands);
        var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(
            command.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(
            (faultedAt + Duration.FromMinutes(1)).ToDateTimeOffset(),
            payload!.FaultEvidenceDeadline);
        Assert.Equal(EWorkerCommandStatus.CancelledByCostStop, command.Status);
        Assert.Equal(2, provider.DiscoveryCalls);
    }

    private static LifecycleJobExecutor CreateExecutor(
        HostingTestStore store,
        IRuntimeProvider provider,
        IHostingCostSliceService costSliceService,
        IGameServerDefinitionCatalog? gameServerDefinitions = null,
        LifecycleWorkerOptions? workerOptions = null,
        IWorkerBootstrapPayloadRenderer? bootstrapRenderer = null)
        => CreateExecutor(
            store.DbContext,
            provider,
            costSliceService,
            gameServerDefinitions,
            workerOptions,
            bootstrapRenderer);

    private static LifecycleJobExecutor CreateExecutor(
        DatabaseContext dbContext,
        IRuntimeProvider provider,
        IHostingCostSliceService costSliceService,
        IGameServerDefinitionCatalog? gameServerDefinitions = null,
        LifecycleWorkerOptions? workerOptions = null,
        IWorkerBootstrapPayloadRenderer? bootstrapRenderer = null)
        => new(
            dbContext,
            gameServerDefinitions ?? new TestGameServerDefinitionCatalog(),
            [provider],
            new WorkerCredentialService(Options.Create(new LifecycleWorkerOptions())),
            bootstrapRenderer ?? new TestBootstrapRenderer(),
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            new GameServerContentSnapshotResolver(dbContext),
            new GameServerConfigurationService(dbContext),
            new EmptyServerFileStorage(),
            new TestStorageDemandService(),
            new HostingAuthorizationService(dbContext),
            costSliceService,
            new HostingAuditWriter(),
            new TestGameServerSecretService(),
            Options.Create(workerOptions ?? new LifecycleWorkerOptions
            {
                RegistrationGrace = TimeSpan.FromMinutes(15),
                BootstrapCredentialLifetime = TimeSpan.FromMinutes(45),
                IdlePollInterval = TimeSpan.FromMilliseconds(5),
             }),
            Options.Create(new HostingFinanceOptions()));

    private sealed class EmptyServerFileStorage : IServerFileStorage
    {
        public Task<IReadOnlyList<ServerFileEntry>> BrowseAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServerFileEntry>>([]);

        public Task<ServerFileDownload> OpenReadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ServerFileEntry> UploadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, Stream content,
            bool overwrite, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ServerFileEntry> CreateDirectoryAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, bool recursive,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static async Task<Exception?> CaptureExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task<(HostingTestSeed Seed, LifecycleJob Job)> SeedRunningStartAsync(HostingTestStore store)
        => await SeedRunningStartAsync(store.DbContext);

    private static async Task<(HostingTestSeed Seed, LifecycleJob Job)> SeedRunningStartAsync(DatabaseContext dbContext)
    {
        var seed = await HostingTestData.SeedServerAsync(
            dbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Provisioning);
        seed.Server.RuntimeProviderIdentifier = ExecutorTestProvider.Identifier;
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampStarted = null;
        seed.User.Claims!.Add(new Claim
        {
            Identifier = Claims.ResourceBased.Server.Operate,
            Value = seed.Server.PrimaryKey.ToString(),
            Category = "Game servers",
            Title = Claims.ResourceBased.Server.Operate,
            Description = "Test operation permission.",
        });
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.LifecycleJobs.Add(job);
        await dbContext.SaveChangesAsync();
        return (seed, job);
    }

    private sealed class ProvisioningClaimCommittedInterceptor(Func<Task> onClaimCommitted)
        : DbTransactionInterceptor
    {
        private int _handled;

        public override async Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var hasProvisioningClaim = eventData.Context?.ChangeTracker
                .Entries<DeploymentResource>()
                .Any(q => q.Entity.TimeStampProvisioningRequested.HasValue) == true;
            if (!hasProvisioningClaim || Interlocked.Exchange(ref _handled, 1) != 0)
                return;
            await onClaimCommitted();
        }
    }

    private static async Task<(HostingTestSeed Seed, LifecycleJob Job, LifecycleWorker Worker)> SeedTerminateAsync(
        HostingTestStore store,
        bool faultTriggered)
    {
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: faultTriggered
                ? EDeploymentStatus.TerminatingDueToFault
                : EDeploymentStatus.Running);
        seed.Server.RuntimeProviderIdentifier = ExecutorTestProvider.Identifier;
        seed.Deployment!.IsProvisioningBlocked = faultTriggered;
        seed.Deployment.FaultCode = faultTriggered ? "test-fault" : null;
        seed.Deployment.FaultMessage = faultTriggered ? "A confirmed test fault occurred." : null;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Deployment.TimeStampFaulted = faultTriggered ? now : null;
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment.PrimaryKey,
            Identity = $"worker-{seed.Deployment.PrimaryKey:N}",
            WorkerCredentialHash = new string('a', 64),
            WorkerCredentialIssuedAt = now,
            WorkerCredentialExpiresAt = now + Duration.FromHours(1),
            WorkerCredentialActivatedAt = now,
            TimeStampRegistered = now,
            Status = EWorkerStatus.Online,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            RequestedByFk = faultTriggered ? null : seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Terminate,
            ActionIdentifier = GameServerActions.Terminate,
            Sequence = 1,
            Priority = int.MaxValue,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = int.MaxValue,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        seed.Deployment.Workers.Add(worker);
        store.DbContext.AddRange(worker, job);
        await store.DbContext.SaveChangesAsync();
        return (seed, job, worker);
    }

    private sealed class TestBootstrapRenderer : IWorkerBootstrapPayloadRenderer
    {
        public WorkerBootstrapPayload Render(
            WorkerBootstrapConfiguration bootstrap,
            Guid serverId,
            string gameIdentifier,
            IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
            => new("#cloud-config\nwrite_files: []");
    }

    private sealed class ThrowingBootstrapRenderer : IWorkerBootstrapPayloadRenderer
    {
        public WorkerBootstrapPayload Render(
            WorkerBootstrapConfiguration bootstrap,
            Guid serverId,
            string gameIdentifier,
            IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
            => throw new InvalidOperationException("The immutable worker image is invalid.");
    }

    private sealed class RecordingCostSliceService : IHostingCostSliceService
    {
        public int ReserveCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public int ReleaseCalls { get; private set; }

        public Exception? Failure { get; init; }

        public Exception? CommitFailure { get; init; }

        public Task ReserveInitialSliceAsync(
            Guid deploymentId,
            string providerIdentifier,
            CancellationToken cancellationToken = default)
        {
            ReserveCalls++;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task CommitInitialReservationAsync(
            Guid deploymentId,
            string providerIdentifier,
            CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            return CommitFailure is null ? Task.CompletedTask : Task.FromException(CommitFailure);
        }

        public Task ReleaseInitialReservationAsync(
            Guid deploymentId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            ReleaseCalls++;
            return Task.CompletedTask;
        }

        public Task AuthorizeNextSliceAsync(
            Guid deploymentId,
            string providerIdentifier,
            bool enforceAffordability,
            CancellationToken cancellationToken = default)
        {
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class ExecutorTestProvider : IRuntimeProvider
    {
        private readonly HashSet<string> _releasedResourceIdentifiers = new(StringComparer.Ordinal);
        private readonly List<RuntimeResource> _allocatedResources = [];

        /// <summary>Gets whether provisioning returns complete, ready resource graphs.</summary>
        /// <value>Whether the provider should return ready resources.</value>
        public bool AllocateReadyResources { get; init; }

        /// <summary>Gets whether the auxiliary create should return an ambiguous result.</summary>
        /// <value>Whether to simulate a lost response to the auxiliary create.</value>
        public bool FailAuxiliaryCreate { get; init; }

        /// <summary>Gets all provider create requests in the order received.</summary>
        /// <value>The captured requests in provider call order.</value>
        public List<RuntimeProvisionRequest> ProvisionRequests { get; } = [];

        public const string Identifier = "executor-test-cloud";

        public string ProviderIdentifier => Identifier;

        public RuntimeProviderCapabilities Capabilities { get; } = new(
            SupportsStableProvisioningIntent: true,
            SupportsProviderWideDiscovery: true,
            SupportsVerifiedRelease: true,
            SupportsPlatformManagedDanglingRelease: true,
            IsReady: true,
            EstimatedHourlyCostEur: 0.10m,
            RequiredProvisionedResourceTypes: ["server", "primary-ip", "firewall"]);

        public bool ReturnPartialFirewallOnUnknown { get; init; }

        public bool ReturnPartialServerOnUnknown { get; init; }

        public bool ReturnDuplicateServersAfterUnknown { get; init; }

        public bool ReturnCompletePendingGraphAfterUnknown { get; init; }

        public bool ReturnCompleteConflictingGraphAfterUnknown { get; init; }

        public bool ReturnCompleteReadyGraphAfterUnknown { get; set; }

        public bool ProvisionAttemptDefinitivelyClosed { get; init; }

        public bool IgnoreProvisionCancellation { get; init; }

        public Func<Task>? OnProvisionAsync { get; init; }

        public Func<int, Task>? OnDiscoveryAsync { get; init; }

        public ERuntimeProviderOutcome ProvisionOutcome { get; init; } = ERuntimeProviderOutcome.Unknown;

        public int ProvisionCalls { get; private set; }

        public int DiscoveryCalls { get; private set; }

        public int ReleaseCalls { get; private set; }

        public RuntimeProvisionRequest? LastProvisionRequest { get; private set; }

        public async Task<RuntimeProvisionResult> ProvisionAsync(
            RuntimeProvisionRequest request,
            CancellationToken cancellationToken = default)
        {
            ProvisionCalls++;
            LastProvisionRequest = request;
            ProvisionRequests.Add(request);
            if (OnProvisionAsync is not null)
                await OnProvisionAsync();
            if (!IgnoreProvisionCancellation)
                cancellationToken.ThrowIfCancellationRequested();
            if (AllocateReadyResources && (!FailAuxiliaryCreate || ProvisionCalls == 1))
            {
                var server = CreateResource(request, $"server:{ProvisionCalls}", "server", true);
                var graph = new[]
                {
                    server,
                    CreateResource(request, $"primary-ip:{ProvisionCalls}", "primary-ip", true),
                    CreateResource(request, $"firewall:{ProvisionCalls}", "firewall", false),
                };
                _allocatedResources.AddRange(graph);
                return new RuntimeProvisionResult(
                    ERuntimeProviderOutcome.Succeeded, "allocated", null, server, graph);
            }
            var partialResources = new List<RuntimeResource>();
            if (ReturnPartialFirewallOnUnknown)
            {
                partialResources.Add(CreateResource(
                    request,
                    "firewall:19",
                    "firewall",
                    potentiallyBillable: false));
            }
            if (ReturnPartialServerOnUnknown)
            {
                // Deliberately omit this resource from DiscoverAsync. This models a concrete create result that is
                // persisted by identifier but cannot be rediscovered through expected-attribution filtering.
                partialResources.Add(CreateResource(
                    request,
                    "server:42",
                    "server",
                    potentiallyBillable: true));
            }
            return new RuntimeProvisionResult(
                ProvisionOutcome,
                ProvisionOutcome is ERuntimeProviderOutcome.PermanentFailure
                    ? "create-rejected"
                    : "create-outcome-unknown",
                ProvisionOutcome is ERuntimeProviderOutcome.PermanentFailure
                    ? "The provider rejected the request without allocating a resource."
                    : "The provider response was lost.",
                null,
                partialResources,
                IsProvisioningAttemptDefinitivelyClosed: ProvisionAttemptDefinitivelyClosed);
        }

        public Task<RuntimeObservationResult> ObserveAsync(
            RuntimeObservationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimeReleaseResult> ReleaseAsync(
            RuntimeReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            ReleaseCalls++;
            _releasedResourceIdentifiers.Add(request.ResourceIdentifier);
            return Task.FromResult(new RuntimeReleaseResult(
                ERuntimeProviderOutcome.Succeeded,
                "released",
                null,
                IsVerifiedNonBillable: true));
        }

        public Task<RuntimeReleaseResult> ReleaseDanglingAsync(
            RuntimeDanglingReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            DiscoveryCalls++;
            if (OnDiscoveryAsync is not null)
                await OnDiscoveryAsync(DiscoveryCalls);
            var resources = _allocatedResources.Where(q => !_releasedResourceIdentifiers.Contains(q.ResourceIdentifier)).ToList();
            if (LastProvisionRequest is { } request)
            {
                if (ReturnPartialFirewallOnUnknown
                    && !ReturnCompleteReadyGraphAfterUnknown
                    && !_releasedResourceIdentifiers.Contains("firewall:19"))
                {
                    resources.Add(CreateResource(request, "firewall:19", "firewall", potentiallyBillable: false));
                }
                if (ReturnDuplicateServersAfterUnknown)
                {
                    foreach (var identifier in new[] { "server:42", "server:43" })
                    {
                        if (!_releasedResourceIdentifiers.Contains(identifier))
                            resources.Add(CreateResource(request, identifier, "server", potentiallyBillable: true));
                    }
                }
                var completeGraphReadiness = ReturnCompleteConflictingGraphAfterUnknown
                    ? ERuntimeResourceProvisioningReadiness.Conflict
                    : ERuntimeResourceProvisioningReadiness.Pending;
                if (ReturnCompletePendingGraphAfterUnknown
                    || ReturnCompleteConflictingGraphAfterUnknown
                    || ReturnCompleteReadyGraphAfterUnknown)
                {
                    foreach (var definition in new[]
                             {
                                 (Identifier: "server:42", Kind: "server", Billable: true),
                                 (Identifier: "primary-ip:91", Kind: "primary-ip", Billable: true),
                                 (Identifier: "firewall:19", Kind: "firewall", Billable: false),
                             })
                    {
                        if (_releasedResourceIdentifiers.Contains(definition.Identifier))
                            continue;
                        resources.Add(CreateResource(
                            request,
                            definition.Identifier,
                            definition.Kind,
                            definition.Billable,
                            ReturnCompleteReadyGraphAfterUnknown || definition.Kind == "server"
                                ? ERuntimeResourceProvisioningReadiness.Ready
                                : completeGraphReadiness));
                    }
                }
            }
            return new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.Succeeded,
                "discovered",
                null,
                resources);
        }

        private static RuntimeResource CreateResource(
            RuntimeProvisionRequest request,
            string identifier,
            string resourceType,
            bool potentiallyBillable,
            ERuntimeResourceProvisioningReadiness provisioningReadiness =
                ERuntimeResourceProvisioningReadiness.Ready)
            => new(
                Identifier,
                identifier,
                IsAllocated: true,
                IsPotentiallyCostGenerating: potentiallyBillable,
                ObservedAt: DateTimeOffset.UtcNow,
                ResourceType: resourceType,
                ProviderState: "allocated",
                PublicAddress: resourceType is "primary-ip" or "server" ? "203.0.113.91" : null,
                Attribution: new RuntimeResourceAttribution(
                    request.ServerId,
                    request.DeploymentId,
                    request.ProvisioningIntentId,
                    request.WorkerId),
                IsPlatformManaged: true,
                HasTrustworthyAttribution: true,
                ProvisioningReadiness: provisioningReadiness,
                OperationalState: ERuntimeResourceOperationalState.Operational);
    }
}
