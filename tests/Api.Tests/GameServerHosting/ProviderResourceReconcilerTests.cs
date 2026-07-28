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
    public void ReconciliationOptions_RejectAbsenceWindowThatCanExpireDuringProviderSettlement()
    {
        var options = new ProviderReconciliationOptions
        {
            AmbiguousCreateAbsenceConfirmationWindow = TimeSpan.FromMinutes(10),
        };

        Assert.Throws<InvalidOperationException>(() =>
            options.ValidateAgainstProviderSettlement(TimeSpan.FromMinutes(5)));
    }

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
        worker.WorkerCredentialHash = new string('a', 64);
        worker.WorkerCredentialIssuedAt = now;
        worker.WorkerCredentialExpiresAt = now + Duration.FromHours(1);
        worker.WorkerCredentialActivatedAt = now;
        AddSucceededStopCommand(store, deployment, worker, now);
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
    public async Task VerifiedInventory_WithLiveStopExecutor_DefersFinalizationUntilLeaseExpires()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Stopping);
        var deployment = seed.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.IsProvisioningBlocked = true;
        deployment.PossibleCost = false;
        var worker = AddWorker(store, deployment, now, stopped: false);
        AddSucceededStopCommand(store, deployment, worker, now);
        var stopJob = store.DbContext.LifecycleJobs.Local.Single(q => q.DeploymentFk == deployment.PrimaryKey);
        stopJob.Status = ELifecycleJobStatus.Running;
        stopJob.LeaseOwner = "live-test-executor";
        stopJob.LeaseExpires = now + Duration.FromHours(1);
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
            ProviderResourceIdentifier = "server:already-released",
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
        var reconciler = CreateReconciler(store, new ReconciliationTestProvider([]));

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Stopping, deployment.Status);
        Assert.Null(deployment.TimeStampResolved);
        Assert.Null(worker.TimeStampStopped);

        stopJob.LeaseExpires = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromSeconds(1);
        await store.DbContext.SaveChangesAsync();
        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        await store.DbContext.Entry(worker).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Stopped, deployment.Status);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.NotNull(worker.TimeStampStopped);
    }

    [Fact]
    public async Task TerminatingDeployment_WithIssuedUnobservedIntent_FinalizesAfterTwoEmptyConfirmations()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.TerminatingDueToFault);
        var deployment = seed.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.IsProvisioningBlocked = true;
        deployment.PossibleCost = true;
        var worker = AddWorker(store, deployment, now, stopped: false);
        var intent = new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
            Kind = "server",
            State = EDeploymentResourceState.Unknown,
            PossibleCost = true,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
        };
        store.DbContext.DeploymentResources.Add(intent);
        await store.DbContext.SaveChangesAsync();
        var provider = new ReconciliationTestProvider([]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        await store.DbContext.Entry(intent).ReloadAsync();
        Assert.Null(deployment.TimeStampResolved);
        Assert.Null(intent.TimeStampProvisioningResolved);
        Assert.NotNull(intent.TimeStampObserved);
        intent.TimeStampObserved = now - Duration.FromMinutes(20);
        await store.DbContext.SaveChangesAsync();

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        await store.DbContext.Entry(intent).ReloadAsync();
        await store.DbContext.Entry(worker).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.NotNull(intent.TimeStampProvisioningResolved);
        Assert.NotNull(intent.TimeStampReleased);
        Assert.False(intent.PossibleCost);
        Assert.Equal(EWorkerStatus.Offline, worker.Status);
        Assert.NotNull(worker.TimeStampStopped);
        Assert.Equal(3, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task IssuedIntent_PositivePartialObservation_ResetsEmptyConfirmationWindow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.TerminatingDueToFault);
        var deployment = seed.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.IsProvisioningBlocked = true;
        deployment.PossibleCost = true;
        var worker = AddWorker(store, deployment, now, stopped: false);
        var intentId = Guid.NewGuid();
        var intent = new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            Kind = "server",
            State = EDeploymentResourceState.Unknown,
            PossibleCost = true,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
        };
        store.DbContext.DeploymentResources.Add(intent);
        await store.DbContext.SaveChangesAsync();
        var partial = Resource(
            "firewall:late-partial",
            "firewall",
            new RuntimeResourceAttribution(
                seed.Server.PrimaryKey,
                deployment.PrimaryKey,
                intentId,
                worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true);
        var provider = new ReconciliationTestProvider(
            [],
            discoverySequence: [[], [partial], []]);
        var reconciler = CreateReconciler(store, provider);

        await reconciler.ReconcileAllAsync();
        intent.TimeStampObserved = now - Duration.FromMinutes(20);
        // Model a competing reconciler that just committed a synthetic two-empty closure. The next positive provider
        // observation must reopen both the intent and terminal deployment before any fresh absence window can start.
        intent.State = EDeploymentResourceState.Released;
        intent.PossibleCost = false;
        intent.TimeStampProvisioningResolved = now;
        intent.TimeStampReleased = now;
        deployment.Status = EDeploymentStatus.TerminatedDueToFault;
        deployment.PossibleCost = false;
        deployment.TimeStampResolved = now;
        await store.DbContext.SaveChangesAsync();

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        await store.DbContext.Entry(intent).ReloadAsync();
        Assert.Null(intent.TimeStampObserved);
        Assert.Null(intent.TimeStampProvisioningResolved);
        Assert.Null(intent.TimeStampReleased);
        Assert.Null(deployment.TimeStampResolved);
        Assert.Equal(EDeploymentStatus.TerminatingDueToFault, deployment.Status);
        Assert.Contains(provider.LinkedReleaseRequests, q => q.ResourceIdentifier == "firewall:late-partial");

        await reconciler.ReconcileAllAsync();

        await store.DbContext.Entry(intent).ReloadAsync();
        Assert.NotNull(intent.TimeStampObserved);
        Assert.Null(intent.TimeStampProvisioningResolved);
        Assert.Null(deployment.TimeStampResolved);
        Assert.Equal(3, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task FinalVerification_WhenUserTerminateCommitsAfterInitialRelease_FinalizesAsTerminate()
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
        worker.WorkerCredentialHash = new string('a', 64);
        worker.WorkerCredentialIssuedAt = now;
        worker.WorkerCredentialExpiresAt = now + Duration.FromHours(1);
        worker.WorkerCredentialActivatedAt = now;
        AddSucceededStopCommand(store, deployment, worker, now);
        var intentId = Guid.NewGuid();
        store.DbContext.DeploymentResources.Add(new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = ReconciliationTestProvider.Identifier,
            ProvisioningIntentIdentifier = intentId.ToString("N"),
            ProviderResourceIdentifier = "server:stop-terminate-race",
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
            "server:stop-terminate-race",
            "server",
            new RuntimeResourceAttribution(seed.Server.PrimaryKey, deployment.PrimaryKey, intentId, worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true);
        var provider = new ReconciliationTestProvider(
            [observed],
            onDiscoveryAsync: async discoveryCall =>
            {
                if (discoveryCall != 2)
                    return;
                // Use raw SQL intentionally: the reconciler's tracked deployment remains stale until its lock-time
                // reload, matching a Terminate committed by another request context.
                const string terminationStage = "termination-requested";
                await store.DbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "GameServerDeployments"
                     SET "Status" = {EDeploymentStatus.Terminating}, "Stage" = {terminationStage}
                     WHERE "PrimaryKey" = {deployment.PrimaryKey}
                     """);
            });

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Terminated, deployment.Status);
        Assert.Equal("terminated", deployment.Stage);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.NotNull(worker.WorkerCredentialRevokedAt);
        Assert.Equal(2, provider.DiscoveryCalls);
    }

    [Theory]
    [InlineData(EDeploymentStatus.Running)]
    [InlineData(EDeploymentStatus.Stopping)]
    [InlineData(EDeploymentStatus.PreservationFailed)]
    public async Task OrdinaryStopBeforeSuccessfulPreservation_DoesNotReleaseProviderResources(
        EDeploymentStatus status)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: status);
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
            ProviderResourceIdentifier = "server:preservation-pending",
            Kind = "server",
            State = EDeploymentResourceState.Allocated,
            PossibleCost = true,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
            TimeStampProvisioningResolved = now,
        });
        await store.DbContext.SaveChangesAsync();
        var observed = Resource(
            "server:preservation-pending",
            "server",
            new RuntimeResourceAttribution(
                seed.Server.PrimaryKey,
                deployment.PrimaryKey,
                intentId,
                worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true);
        var provider = new ReconciliationTestProvider([observed]);

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(status, deployment.Status);
        Assert.Null(deployment.TimeStampResolved);
        Assert.True(deployment.PossibleCost);
        Assert.Empty(provider.LinkedReleaseRequests);
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
    public async Task RunningDeployment_WithPendingProviderRelationship_RemainsRunningWithoutRelease()
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
            ProviderResourceIdentifier = "primary-ip:pending",
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
            "primary-ip:pending",
            "primary-ip",
            new RuntimeResourceAttribution(
                seed.Server.PrimaryKey,
                deployment.PrimaryKey,
                intentId,
                worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true,
            provisioningReadiness: ERuntimeResourceProvisioningReadiness.Pending);
        var provider = new ReconciliationTestProvider([observed]);

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.Running, deployment.Status);
        Assert.False(deployment.IsProvisioningBlocked);
        Assert.Null(deployment.FaultCode);
        Assert.Null(deployment.TimeStampResolved);
        Assert.Empty(provider.LinkedReleaseRequests);
    }

    [Fact]
    public async Task RunningDeployment_WithConfirmedProviderOperationalFault_FaultStopsAndReleasesGraph()
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
            ProviderResourceIdentifier = "server:faulted",
            Kind = "server",
            State = EDeploymentResourceState.Allocated,
            PossibleCost = true,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampProvisioningRequested = now,
            TimeStampProvisioningResolved = now,
        });
        await store.DbContext.SaveChangesAsync();
        var observed = Resource(
            "server:faulted",
            "server",
            new RuntimeResourceAttribution(seed.Server.PrimaryKey, deployment.PrimaryKey, intentId, worker.PrimaryKey),
            isManaged: true,
            isTrustworthy: true,
            providerState: "off",
            operationalState: ERuntimeResourceOperationalState.Faulted);
        var provider = new ReconciliationTestProvider([observed]);

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-resource-operational-fault", deployment.FaultCode);
        Assert.True(deployment.IsProvisioningBlocked);
        Assert.NotNull(deployment.TimeStampResolved);
        Assert.False(deployment.PossibleCost);
        Assert.Single(provider.LinkedReleaseRequests);
    }

    [Fact]
    public async Task RunningDeployment_WithDuplicateProviderKind_FaultStopsAndReleasesEveryDuplicate()
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
        var observed = new[]
        {
            Resource(
                "server:duplicate-a",
                "server",
                new RuntimeResourceAttribution(
                    seed.Server.PrimaryKey,
                    deployment.PrimaryKey,
                    intentId,
                    worker.PrimaryKey),
                isManaged: true,
                isTrustworthy: true),
            Resource(
                "server:duplicate-b",
                "server",
                new RuntimeResourceAttribution(
                    seed.Server.PrimaryKey,
                    deployment.PrimaryKey,
                    intentId,
                    worker.PrimaryKey),
                isManaged: true,
                isTrustworthy: true),
        };
        foreach (var resource in observed)
        {
            store.DbContext.DeploymentResources.Add(new DeploymentResource
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = deployment.PrimaryKey,
                ProviderIdentifier = ReconciliationTestProvider.Identifier,
                ProvisioningIntentIdentifier = intentId.ToString("N"),
                ProviderResourceIdentifier = resource.ResourceIdentifier,
                Kind = "server",
                State = EDeploymentResourceState.Allocated,
                PossibleCost = true,
                TimeStampCreated = now,
                TimeStampUpdated = now,
                TimeStampProvisioningRequested = now,
                TimeStampProvisioningResolved = now,
            });
        }
        await store.DbContext.SaveChangesAsync();
        var provider = new ReconciliationTestProvider(observed);

        await CreateReconciler(store, provider).ReconcileAllAsync();

        await store.DbContext.Entry(deployment).ReloadAsync();
        Assert.Equal(EDeploymentStatus.TerminatedDueToFault, deployment.Status);
        Assert.Equal("provider-resource-graph-duplicate", deployment.FaultCode);
        Assert.Equal(2, provider.LinkedReleaseRequests.Count);
        Assert.All(store.DbContext.DeploymentResources, q => Assert.NotNull(q.TimeStampReleased));
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
        AddSucceededStopCommand(store, deployment, worker, now);
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
    {
        var auditWriter = new HostingAuditWriter();
        return new ProviderResourceReconciler(
            store.DbContext,
            [provider],
            auditWriter,
            new AutomaticCostStopService(store.DbContext, auditWriter),
            new WorkerCredentialService(Options.Create(new LifecycleWorkerOptions())),
            Options.Create(new ProviderReconciliationOptions
            {
                InitialDelay = TimeSpan.Zero,
                ScanInterval = TimeSpan.FromMinutes(1),
                AmbiguousCreateAbsenceConfirmationWindow = TimeSpan.FromMinutes(15),
                CompletedIncidentRetention = TimeSpan.FromDays(365),
            }),
            Options.Create(new LifecycleWorkerOptions()),
            NullLogger<ProviderResourceReconciler>.Instance);
    }

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

    private static void AddSucceededStopCommand(
        HostingTestStore store,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        Instant now)
    {
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = deployment.GameServerFk,
            DeploymentFk = deployment.PrimaryKey,
            Kind = ELifecycleJobKind.Stop,
            ActionIdentifier = GameServerActions.Stop,
            Sequence = 1,
            Priority = 50,
            Status = ELifecycleJobStatus.RetryPending,
            Stage = "provider-teardown",
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStarted = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJobFk = job.PrimaryKey,
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
        job.WorkerCommands.Add(command);
        store.DbContext.LifecycleJobs.Add(job);
        store.DbContext.WorkerCommands.Add(command);
    }

    private static RuntimeResource Resource(
        string identity,
        string resourceType,
        RuntimeResourceAttribution? attribution,
        bool isManaged,
        bool isTrustworthy,
        string? publicAddress = null,
        ERuntimeResourceProvisioningReadiness provisioningReadiness =
            ERuntimeResourceProvisioningReadiness.Ready,
        string providerState = "allocated",
        ERuntimeResourceOperationalState operationalState = ERuntimeResourceOperationalState.Uncertain)
        => new(
            ReconciliationTestProvider.Identifier,
            identity,
            IsAllocated: true,
            IsPotentiallyCostGenerating: true,
            ObservedAt: DateTimeOffset.UtcNow,
            ResourceType: resourceType,
            ProviderState: providerState,
            PublicAddress: publicAddress,
            Attribution: attribution,
            ProviderLabels: new Dictionary<string, string>(),
            IsPlatformManaged: isManaged,
            HasTrustworthyAttribution: isTrustworthy,
            ProvisioningReadiness: provisioningReadiness,
            OperationalState: operationalState);

    private sealed class ReconciliationTestProvider(
        IReadOnlyList<RuntimeResource> discovered,
        IEnumerable<RuntimeReleaseResult>? danglingReleaseResults = null,
        IEnumerable<IReadOnlyList<RuntimeResource>>? discoverySequence = null,
        Func<int, Task>? onDiscoveryAsync = null) : IRuntimeProvider
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

        public async Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            DiscoveryCalls++;
            if (onDiscoveryAsync is not null)
                await onDiscoveryAsync(DiscoveryCalls);
            var current = _discoverySequence.Count > 1
                ? _discoverySequence.Dequeue()
                : _discoverySequence.Peek();
            var released = LinkedReleaseRequests
                .Select(q => q.ResourceIdentifier)
                .Concat(DanglingReleaseRequests.Select(q => q.ResourceIdentifier))
                .ToHashSet(StringComparer.Ordinal);
            return new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.Succeeded,
                "discovered",
                null,
                current.Where(q => !released.Contains(q.ResourceIdentifier)).ToArray());
        }
    }
}
