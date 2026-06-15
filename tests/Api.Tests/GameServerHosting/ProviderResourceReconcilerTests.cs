using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class ProviderResourceReconcilerTests
{
    [Fact]
    public async Task LateLinkedResource_ReopensResolvedDeploymentAndCleansItAutomatically()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Terminated);
        var deployment = seed.Deployment!;
        var now = Instant.FromUnixTimeSeconds(1_700_000_100);
        deployment.TimeStampResolved = now;
        deployment.PossibleCost = false;
        deployment.IsProvisioningBlocked = true;
        var worker = AddWorker(store, deployment, now, stopped: true);
        var intentId = Guid.NewGuid();
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            Kind = "server",
            State = EDeploymentResourceState.Released,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
            TimeStampProvisioningResolved = now,
            TimeStampReleased = now,
        });
        await store.DbContext.SaveChangesAsync();

        var observed = Resource(
            "server:late",
            "server",
            new RuntimeResourceAttribution(seed.Server.PrimaryKey, deployment.PrimaryKey, intentId, worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true,
            publicAddress: "203.0.113.91");
        var provider = new ReconciliationTestProvider([observed]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();

        var adopted = await store.DbContext.DeploymentResources
            .SingleAsync(q => q.ProviderResourceIdentifier == "server:late");
        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentResourceState.Released, adopted.State);
        Assert.NotNull(adopted.TimeStampReleased);
        Assert.False(adopted.PossibleCost);
        Assert.Equal("allocated", adopted.ProviderState);
        Assert.Equal("203.0.113.91", adopted.PublicAddress);
        Assert.Equal(EDeploymentStatus.Terminated, deployment.Status);
        Assert.Equal("late-provider-resource-cleanup-complete", deployment.Stage);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Single(provider.LinkedReleaseRequests);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.DeploymentResourceAdopted);
    }

    [Fact]
    public async Task LateLinkedResource_DoesNotReopenWhenServerHasNewerActiveDeployment()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Terminated);
        var oldDeployment = seed.Deployment!;
        var resolvedAt = Instant.FromUnixTimeSeconds(1_700_000_100);
        oldDeployment.TimeStampResolved = resolvedAt;
        oldDeployment.PossibleCost = false;
        oldDeployment.IsProvisioningBlocked = true;
        var oldWorker = AddWorker(store, oldDeployment, resolvedAt, stopped: true);
        var intentId = Guid.NewGuid();
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = oldDeployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            Kind = "server",
            State = EDeploymentResourceState.Released,
            TimeStampCreated = resolvedAt,
            TimeStampUpdated = resolvedAt,
            TimeStampProvisioningRequested = resolvedAt,
            TimeStampProvisioningResolved = resolvedAt,
            TimeStampReleased = resolvedAt,
        });
        await store.DbContext.SaveChangesAsync();
        store.DbContext.GameServerDeployments.Add(new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            Status = EDeploymentStatus.Running,
            Stage = "running",
            PossibleCost = true,
            TimeStampCreated = resolvedAt + Duration.FromMinutes(1),
            TimeStampUpdated = resolvedAt + Duration.FromMinutes(1),
        });
        await store.DbContext.SaveChangesAsync();

        var observed = Resource(
            "server:late-old",
            "server",
            new RuntimeResourceAttribution(
                seed.Server.PrimaryKey,
                oldDeployment.PrimaryKey,
                intentId,
                oldWorker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true);
        var provider = new ReconciliationTestProvider([observed]);

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(oldDeployment).ReloadAsync();
        Assert.Equal(resolvedAt, oldDeployment.TimeStampResolved);
        Assert.Equal(EDeploymentStatus.Terminated, oldDeployment.Status);
        Assert.False(oldDeployment.PossibleCost);
        Assert.Equal("late-provider-resource-cleanup-complete", oldDeployment.Stage);
        Assert.Single(provider.LinkedReleaseRequests);
    }

    [Fact]
    public async Task VerifiedLastRelease_FinalizesStoppingDeploymentAndRevokesWorker()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Stopping);
        var deployment = seed.Deployment!;
        var now = Instant.FromUnixTimeSeconds(1_700_000_100);
        deployment.IsProvisioningBlocked = true;
        deployment.PossibleCost = true;
        var worker = AddWorker(store, deployment, now, stopped: false);
        var intentId = Guid.NewGuid();
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            ProviderResourceIdentifier = "server:stopping",
            Kind = "server",
            State = EDeploymentResourceState.ReleasePending,
            PossibleCost = true,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
            TimeStampProvisioningResolved = now,
        });
        await store.DbContext.SaveChangesAsync();
        var observed = Resource(
            "server:stopping",
            "server",
            new RuntimeResourceAttribution(seed.Server.PrimaryKey, deployment.PrimaryKey, intentId, worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true);

        await CreateReconciler(store, new ReconciliationTestProvider([observed])).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        await store.DbContext.Entry(worker).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Stopped, deployment.Status);
        Assert.Equal("stopped", deployment.Stage);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Equal(EWorkerStatus.Offline, worker.Status);
        Assert.NotNull(worker.TimeStampStopped);
    }

    [Fact]
    public async Task ManagedUnlinkableResource_PersistsIncidentBeforeVerifiedCleanup()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var observed = Resource(
            "primary-ip:91",
            "primary-ip",
            new RuntimeResourceAttribution(null, null, null, null),
            isManaged: true,
            isTrustworthy: false);
        var provider = new ReconciliationTestProvider([observed]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();

        var incident = await store.DbContext.DanglingProviderResourceIncidents.SingleAsync();
        Assert.Equal(EDanglingResourceIncidentStatus.Resolved, incident.Status);
        Assert.True(incident.IsPlatformManaged);
        Assert.False(incident.PossibleCost);
        Assert.Equal(1, incident.CleanupAttemptCount);
        Assert.Single(provider.DanglingReleaseRequests);
        Assert.Empty(store.DbContext.DeploymentResources);
        var auditKinds = await store.DbContext.HostingAuditRecords.Select(q => q.Kind).ToArrayAsync();
        Assert.Contains(EHostingAuditEventKind.DanglingResourceDetected, auditKinds);
        Assert.Contains(EHostingAuditEventKind.DanglingResourceCleanupSucceeded, auditKinds);
    }

    [Fact]
    public async Task UnmanagedUnlinkableResource_IsIgnoredAndNeverDeleted()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var observed = Resource(
            "server:external",
            "server",
            null,
            isManaged: false,
            isTrustworthy: false);
        var provider = new ReconciliationTestProvider([observed]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();

        Assert.Empty(provider.DanglingReleaseRequests);
        Assert.Empty(await store.DbContext.DanglingProviderResourceIncidents.ToArrayAsync());
        Assert.Empty(await store.DbContext.DeploymentResources.ToArrayAsync());
    }

    [Fact]
    public async Task FailedDanglingCleanup_RemainsDurableAndRetriesUntilVerified()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var observed = Resource(
            "firewall:19",
            "firewall",
            null,
            isManaged: true,
            isTrustworthy: false);
        var provider = new ReconciliationTestProvider(
            [observed],
            [
                new RuntimeReleaseResult(
                    ERuntimeProviderOutcome.TransientFailure,
                    "delete-transient",
                    "provider unavailable",
                    false),
                new RuntimeReleaseResult(
                    ERuntimeProviderOutcome.Succeeded,
                    "released-verified-absent",
                    null,
                    true),
            ]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();
        var afterFailure = await store.DbContext.DanglingProviderResourceIncidents.SingleAsync();
        Assert.Equal(EDanglingResourceIncidentStatus.CleanupFailed, afterFailure.Status);
        Assert.True(afterFailure.PossibleCost);

        await reconciler.ReconcileAllAsync();

        var resolved = await store.DbContext.DanglingProviderResourceIncidents.SingleAsync();
        Assert.Equal(EDanglingResourceIncidentStatus.Resolved, resolved.Status);
        Assert.Equal(2, resolved.CleanupAttemptCount);
        Assert.Equal(2, provider.DanglingReleaseRequests.Count);
        Assert.Equal(
            1,
            await store.DbContext.HostingAuditRecords.CountAsync(q =>
                q.Kind == EHostingAuditEventKind.DanglingResourceDetected));
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.DanglingResourceCleanupFailed);
    }

    [Fact]
    public async Task RunningDeployment_WithProviderRelationshipConflict_FaultStopsAndReleasesGraph()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Running);
        var deployment = seed.Deployment!;
        var now = Instant.FromUnixTimeSeconds(1_700_000_100);
        var worker = AddWorker(store, deployment, now, stopped: false);
        var intentId = Guid.NewGuid();
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            ProviderResourceIdentifier = "primary-ip:unsafe",
            Kind = "primary-ip",
            State = EDeploymentResourceState.Allocated,
            PossibleCost = true,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
            TimeStampProvisioningResolved = now,
        });
        await store.DbContext.SaveChangesAsync();
        var observed = Resource(
            "primary-ip:unsafe",
            "primary-ip",
            new RuntimeResourceAttribution(
                seed.Server.PrimaryKey,
                deployment.PrimaryKey,
                intentId,
                worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true,
            provisioningReadiness: ERuntimeResourceProvisioningReadiness.Conflict);
        var provider = new ReconciliationTestProvider([observed]);

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-resource-graph-drift", deployment.FaultCode);
        Assert.True(deployment.IsProvisioningBlocked);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Single(provider.LinkedReleaseRequests);
        Assert.Equal(2, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task Teardown_FinalVerificationFindsLateResource_AndDefersResolutionUntilCleanup()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Stopping);
        var deployment = seed.Deployment!;
        var now = Instant.FromUnixTimeSeconds(1_700_000_100);
        deployment.IsProvisioningBlocked = true;
        deployment.PossibleCost = false;
        var worker = AddWorker(store, deployment, now, stopped: true);
        var intentId = Guid.NewGuid();
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            ProviderResourceIdentifier = "server:released",
            Kind = "server",
            State = EDeploymentResourceState.Released,
            PossibleCost = false,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
            TimeStampProvisioningResolved = now,
            TimeStampReleased = now,
        });
        await store.DbContext.SaveChangesAsync();
        var late = Resource(
            "server:late",
            "server",
            new RuntimeResourceAttribution(
                seed.Server.PrimaryKey,
                deployment.PrimaryKey,
                intentId,
                worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true);
        var provider = new ReconciliationTestProvider(
            [],
            discoverySequence:
            [
                Array.Empty<RuntimeResource>(),
                new[] { late },
            ]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Null(deployment.TimeStampResolved);
        Assert.Equal(EDeploymentStatus.Stopping, deployment.Status);
        Assert.Equal("provider-final-verification-found-resource", deployment.Stage);
        Assert.True(deployment.PossibleCost);
        Assert.Empty(provider.LinkedReleaseRequests);

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Stopped, deployment.Status);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Contains(provider.LinkedReleaseRequests, q => q.ResourceIdentifier == "server:late");
        Assert.Equal(4, provider.DiscoveryCalls);
    }

    private static ProviderResourceReconciler CreateReconciler(
        HostingTestStore store,
        IRuntimeProvider provider)
        => new(
            store.DbContext,
            [provider],
            new HostingAuditWriter(),
            new WorkerCredentialService(Options.Create(new LifecycleWorkerOptions())),
            Options.Create(new ProviderReconciliationOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.Zero,
                ScanInterval = TimeSpan.FromMinutes(1),
                CompletedIncidentRetention = TimeSpan.FromDays(365),
            }),
            NullLogger<ProviderResourceReconciler>.Instance);

    private static LifecycleWorker AddWorker(
        HostingTestStore store,
        GameServerDeployment deployment,
        Instant now,
        bool stopped)
    {
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            Identity = "test-worker",
            Status = stopped ? EWorkerStatus.Offline : EWorkerStatus.Online,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStopped = stopped ? now : null,
        };
        store.DbContext.LifecycleWorkers.Add(worker);
        return worker;
    }

    private static RuntimeResource Resource(
        string identity,
        string resourceType,
        RuntimeResourceAttribution? attribution,
        bool isManaged,
        bool isTrustworthy,
        string? publicAddress = null,
        ERuntimeResourceProvisioningReadiness provisioningReadiness =
            ERuntimeResourceProvisioningReadiness.Ready)
        => new(
            ReconciliationTestProvider.Identifier,
            identity,
            IsAllocated: true,
            IsPotentiallyCostGenerating: true,
            ObservedAt: DateTimeOffset.UtcNow,
            ResourceType: resourceType,
            ProviderState: "allocated",
            PublicAddress: publicAddress,
            Attribution: attribution,
            ProviderLabels: new Dictionary<string, string>(),
            IsPlatformManaged: isManaged,
            HasTrustworthyAttribution: isTrustworthy,
            ProvisioningReadiness: provisioningReadiness);

    private sealed class ReconciliationTestProvider(
        IReadOnlyList<RuntimeResource> discovered,
        IEnumerable<RuntimeReleaseResult>? danglingReleaseResults = null,
        IEnumerable<IReadOnlyList<RuntimeResource>>? discoverySequence = null) : IRuntimeProvider
    {
        public const string Identifier = "reconciliation-test";
        private readonly Queue<RuntimeReleaseResult> _danglingReleaseResults = new(
            danglingReleaseResults ??
            [
                new RuntimeReleaseResult(
                    ERuntimeProviderOutcome.Succeeded,
                    "released-verified-absent",
                    null,
                    true),
            ]);
        private readonly Queue<IReadOnlyList<RuntimeResource>> _discoverySequence = new(
            discoverySequence ?? [discovered]);

        public string ProviderIdentifier => Identifier;

        public RuntimeProviderCapabilities Capabilities { get; } = new(true, true, true, true);

        public List<RuntimeReleaseRequest> LinkedReleaseRequests { get; } = [];

        public List<RuntimeDanglingReleaseRequest> DanglingReleaseRequests { get; } = [];

        public int DiscoveryCalls { get; private set; }

        public Task<RuntimeProvisionResult> ProvisionAsync(
            RuntimeProvisionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimeObservationResult> ObserveAsync(
            RuntimeObservationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimeObservationResult(
                ERuntimeProviderOutcome.NotFound,
                "not-found",
                null,
                null));

        public Task<RuntimeReleaseResult> ReleaseAsync(
            RuntimeReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            LinkedReleaseRequests.Add(request);
            return Task.FromResult(new RuntimeReleaseResult(
                ERuntimeProviderOutcome.Succeeded,
                "released-verified-absent",
                null,
                true));
        }

        public Task<RuntimeReleaseResult> ReleaseDanglingAsync(
            RuntimeDanglingReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            DanglingReleaseRequests.Add(request);
            return Task.FromResult(_danglingReleaseResults.Dequeue());
        }

        public Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            DiscoveryCalls++;
            var current = _discoverySequence.Count > 1
                ? _discoverySequence.Dequeue()
                : _discoverySequence.Peek();
            var released = LinkedReleaseRequests
                .Select(q => q.ResourceIdentifier)
                .Concat(DanglingReleaseRequests.Select(q => q.ResourceIdentifier))
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.Succeeded,
                "discovered",
                null,
                current.Where(q => !released.Contains(q.ResourceIdentifier)).ToArray()));
        }
    }
}
