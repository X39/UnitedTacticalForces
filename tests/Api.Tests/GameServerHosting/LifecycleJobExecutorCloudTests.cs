using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace Api.Tests.GameServerHosting;

public sealed class LifecycleJobExecutorCloudTests
{
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
    public async Task Start_AfterIssuedUnknownCreateWithStableEmptyDiscovery_FaultTearsDownWithoutReposting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job) = await SeedRunningStartAsync(store);
        var provider = new ExecutorTestProvider();
        var cost = new RecordingCostSliceService();
        var executor = CreateExecutor(store, provider, cost);

        await Assert.ThrowsAsync<LifecycleExecutionException>(() => executor.ExecuteAsync(job));
        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(4, provider.DiscoveryCalls);
        Assert.Equal(0, provider.ReleaseCalls);
        Assert.Equal(1, cost.ReserveCalls);
        Assert.Equal(1, cost.CommitCalls);
        var deployment = seed.Deployment!;
        var resource = Assert.Single(store.DbContext.DeploymentResources);
        Assert.NotNull(resource.TimeStampProvisioningRequested);
        Assert.NotNull(resource.TimeStampProvisioningResolved);
        Assert.NotNull(resource.TimeStampReleased);
        Assert.False(resource.PossibleCost);
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.True(deployment.IsProvisioningBlocked);
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-create-not-observed", deployment.FaultCode);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
    }

    [Fact]
    public async Task Start_AfterUnknownCreateWithStablePartialGraph_FaultTearsDownWithoutReposting()
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
        await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() => executor.ExecuteAsync(job));

        Assert.Equal(1, provider.ProvisionCalls);
        Assert.Equal(4, provider.DiscoveryCalls);
        Assert.Equal(1, provider.ReleaseCalls);
        Assert.Equal(1, cost.ReserveCalls);
        Assert.Equal(1, cost.CommitCalls);
        Assert.Equal(2, store.DbContext.DeploymentResources.Count());
        var placeholder = store.DbContext.DeploymentResources.Single(q => q.Kind == "server");
        Assert.Null(placeholder.ProviderResourceIdentifier);
        Assert.NotNull(placeholder.TimeStampProvisioningResolved);
        Assert.NotNull(placeholder.TimeStampReleased);
        Assert.False(placeholder.PossibleCost);
        var firewall = store.DbContext.DeploymentResources.Single(q => q.Kind == "firewall");
        Assert.NotNull(firewall.TimeStampReleased);
        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-resource-graph-incomplete", deployment.FaultCode);
        Assert.False(deployment.PossibleCost);
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
    public async Task UserTerminate_SkipsWorkerPreservationAndPerformsVerifiedProviderTeardown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, job, _) = await SeedTerminateAsync(store, faultTriggered: false);
        var provider = new ExecutorTestProvider();
        var executor = CreateExecutor(store, provider, new RecordingCostSliceService());

        await executor.ExecuteAsync(job);

        var deployment = seed.Deployment!;
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Terminated, deployment.Status);
        Assert.Equal(2, provider.DiscoveryCalls);
        Assert.Empty(store.DbContext.WorkerCommands);
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
        => new(
            store.DbContext,
            gameServerDefinitions ?? new TestGameServerDefinitionCatalog(),
            [provider],
            new WorkerCredentialService(Options.Create(new LifecycleWorkerOptions())),
            bootstrapRenderer ?? new TestBootstrapRenderer(),
            new HostingAuthorizationService(store.DbContext),
            costSliceService,
            new HostingAuditWriter(),
            Options.Create(workerOptions ?? new LifecycleWorkerOptions
            {
                RegistrationGrace = TimeSpan.FromMinutes(15),
                BootstrapCredentialLifetime = TimeSpan.FromMinutes(45),
                IdlePollInterval = TimeSpan.FromMilliseconds(5),
            }));

    private static async Task<(HostingTestSeed Seed, LifecycleJob Job)> SeedRunningStartAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
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
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        return (seed, job);
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
            return Task.CompletedTask;
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

        public bool ReturnDuplicateServersAfterUnknown { get; init; }

        public bool ReturnCompletePendingGraphAfterUnknown { get; init; }

        public bool ReturnCompleteConflictingGraphAfterUnknown { get; init; }

        public ERuntimeProviderOutcome ProvisionOutcome { get; init; } = ERuntimeProviderOutcome.Unknown;

        public int ProvisionCalls { get; private set; }

        public int DiscoveryCalls { get; private set; }

        public int ReleaseCalls { get; private set; }

        public RuntimeProvisionRequest? LastProvisionRequest { get; private set; }

        public Task<RuntimeProvisionResult> ProvisionAsync(
            RuntimeProvisionRequest request,
            CancellationToken cancellationToken = default)
        {
            ProvisionCalls++;
            LastProvisionRequest = request;
            IReadOnlyList<RuntimeResource> partialResources = ReturnPartialFirewallOnUnknown
                ?
                [
                    new RuntimeResource(
                        Identifier,
                        "firewall:19",
                        IsAllocated: true,
                        IsPotentiallyCostGenerating: false,
                        ObservedAt: DateTimeOffset.UtcNow,
                        ResourceType: "firewall",
                        ProviderState: "allocated",
                        Attribution: new RuntimeResourceAttribution(
                            request.ServerId,
                            request.DeploymentId,
                            request.ProvisioningIntentId,
                            request.WorkerId),
                        IsPlatformManaged: true,
                        HasTrustworthyAttribution: true),
                ]
                : [];
            return Task.FromResult(new RuntimeProvisionResult(
                ProvisionOutcome,
                ProvisionOutcome is ERuntimeProviderOutcome.PermanentFailure
                    ? "create-rejected"
                    : "create-outcome-unknown",
                ProvisionOutcome is ERuntimeProviderOutcome.PermanentFailure
                    ? "The provider rejected the request without allocating a resource."
                    : "The provider response was lost.",
                null,
                partialResources));
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

        public Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            DiscoveryCalls++;
            var resources = new List<RuntimeResource>();
            if (LastProvisionRequest is { } request)
            {
                if (ReturnPartialFirewallOnUnknown
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
                if (ReturnCompletePendingGraphAfterUnknown || ReturnCompleteConflictingGraphAfterUnknown)
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
                            definition.Kind == "server"
                                ? ERuntimeResourceProvisioningReadiness.Ready
                                : completeGraphReadiness));
                    }
                }
            }
            return Task.FromResult(new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.Succeeded,
                "discovered",
                null,
                resources));
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
                ProvisioningReadiness: provisioningReadiness);
    }
}
