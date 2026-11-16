using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

public sealed class DeploymentHealthReconcilerTests
{
    [Fact]
    public async Task StaleIdleWorker_QueuesSystemTerminateAndCancelsNormalWork()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        var stale = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(5);
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            Identity = "stale-worker",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = stale,
            ProcessId = 123,
            ProcessStartedAt = stale,
            TimeStampCreated = stale,
            TimeStampUpdated = stale,
            TimeStampRegistered = stale,
        };
        var normalJob = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Restart,
            ActionIdentifier = GameServerActions.Restart,
            Sequence = 1,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            MaxAttempts = 3,
            TimeStampCreated = stale,
            TimeStampUpdated = stale,
        };
        store.DbContext.AddRange(worker, normalJob);
        await store.DbContext.SaveChangesAsync();

        var options = Options.Create(new LifecycleWorkerOptions
        {
            StaleThreshold = TimeSpan.FromSeconds(60),
        });
        var costStop = new AutomaticCostStopService(store.DbContext, new HostingAuditWriter());
        var reconciler = new DeploymentHealthReconciler(store.DbContext, costStop, options);

        await reconciler.ReconcileAsync();
        store.DbContext.ChangeTracker.Clear();

        var persisted = await store.DbContext.GameServerDeployments.SingleAsync(q => q.PrimaryKey == deployment.PrimaryKey);
        Assert.True(persisted.IsProvisioningBlocked);
        Assert.True(persisted.PossibleCost);
        Assert.Equal(EDeploymentStatus.TerminatingDueToFault, persisted.Status);
        Assert.Equal("dedicated-worker-heartbeat-stale", persisted.FaultCode);
        Assert.Equal(
            ELifecycleJobStatus.CancelledByCostStop,
            await store.DbContext.LifecycleJobs
                .Where(q => q.PrimaryKey == normalJob.PrimaryKey)
                .Select(q => q.Status)
                .SingleAsync());
        var terminate = await store.DbContext.LifecycleJobs
            .SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey && q.Kind == ELifecycleJobKind.Terminate);
        Assert.Null(terminate.RequestedByFk);
        Assert.Equal(int.MaxValue, terminate.MaxAttempts);
        Assert.Equal(int.MaxValue, terminate.Priority);
    }

    [Fact]
    public async Task HealthyIdleWorker_DoesNotQueueTermination()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.LifecycleWorkers.Add(new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment!.PrimaryKey,
            Identity = "healthy-worker",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = now,
            ProcessId = 123,
            ProcessStartedAt = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampRegistered = now,
        });
        await store.DbContext.SaveChangesAsync();

        var options = Options.Create(new LifecycleWorkerOptions
        {
            StaleThreshold = TimeSpan.FromSeconds(60),
        });
        var costStop = new AutomaticCostStopService(store.DbContext, new HostingAuditWriter());
        var reconciler = new DeploymentHealthReconciler(store.DbContext, costStop, options);

        await reconciler.ReconcileAsync();

        Assert.False(await store.DbContext.LifecycleJobs.AnyAsync(q => q.Kind == ELifecycleJobKind.Terminate));
    }

    [Fact]
    public async Task HealthyMultiNodeDeployment_DoesNotQueueTermination()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.LifecycleWorkers.AddRange(
            CreateHealthyWorker(seed.Deployment!.PrimaryKey, "game-server", now),
            CreateHealthyWorker(seed.Deployment.PrimaryKey, "headless-client", now));
        await store.DbContext.SaveChangesAsync();
        var options = Options.Create(new LifecycleWorkerOptions
        {
            StaleThreshold = TimeSpan.FromSeconds(60),
        });
        var costStop = new AutomaticCostStopService(store.DbContext, new HostingAuditWriter());
        var reconciler = new DeploymentHealthReconciler(store.DbContext, costStop, options);

        await reconciler.ReconcileAsync();

        Assert.False(await store.DbContext.LifecycleJobs.AnyAsync(q => q.Kind == ELifecycleJobKind.Terminate));
    }

    /// <summary>Checks both workload observations even when the worker and primary PID remain healthy.</summary>
    /// <param name="mode">Whether companion evidence is healthy, missing, failed, or stale.</param>
    /// <returns>A task completing after validating automatic fault-stop behavior.</returns>
    [Theory]
    [InlineData("healthy")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("failed")]
    [InlineData("stale")]
    public async Task ColocatedWorkloads_RequireFreshRunningEvidence(string mode)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Deployment!.PlacementSnapshot = WorkloadProtocolTestData.Placement();
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = CreateHealthyWorker(seed.Deployment.PrimaryKey, "game-server", now);
        var observations = WorkloadProtocolTestData.Running();
        if (mode == "missing")
            observations = [observations[0]];
        else if (mode == "empty")
            observations = [];
        else if (mode == "failed")
            observations[1] = observations[1] with { State = EWorkerWorkloadState.Failed };
        else if (mode == "stale")
            observations[1] = observations[1] with { ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-10) };
        worker.WorkloadObservations = JsonSerializer.Serialize(observations,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        store.DbContext.LifecycleWorkers.Add(worker);
        await store.DbContext.SaveChangesAsync();
        var reconciler = new DeploymentHealthReconciler(store.DbContext,
            new AutomaticCostStopService(store.DbContext, new HostingAuditWriter()),
            Options.Create(new LifecycleWorkerOptions { StaleThreshold = TimeSpan.FromSeconds(60) }));

        await reconciler.ReconcileAsync();

        Assert.Equal(mode != "healthy", await store.DbContext.LifecycleJobs
            .AnyAsync(job => job.Kind == ELifecycleJobKind.Terminate));
        if (mode != "healthy")
            Assert.Equal("game-workload-missing", seed.Deployment.FaultCode);
    }

    /// <summary>Preserves singular evidence compatibility only for a host with one assigned workload.</summary>
    /// <returns>A task completing after checking that the healthy host is retained.</returns>
    [Fact]
    public async Task SingleAssignedWorkload_AcceptsSingularHeartbeatEvidence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Deployment!.PlacementSnapshot = WorkloadProtocolTestData.Placement(false);
        store.DbContext.LifecycleWorkers.Add(CreateHealthyWorker(seed.Deployment.PrimaryKey, "game-server",
            NodaSystemClock.Instance.GetCurrentInstant()));
        await store.DbContext.SaveChangesAsync();
        var reconciler = new DeploymentHealthReconciler(store.DbContext,
            new AutomaticCostStopService(store.DbContext, new HostingAuditWriter()),
            Options.Create(new LifecycleWorkerOptions { StaleThreshold = TimeSpan.FromSeconds(60) }));

        await reconciler.ReconcileAsync();

        Assert.False(await store.DbContext.LifecycleJobs.AnyAsync(job => job.Kind == ELifecycleJobKind.Terminate));
    }

    /// <summary>Allows commanded process downtime while retaining stale-worker fault detection.</summary>
    /// <param name="stale">Whether the worker has stopped communicating during the command.</param>
    /// <returns>A task completing after checking transition-aware health reconciliation.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveRestart_AllowsMissingProcessesButRequiresWorkerHeartbeat(bool stale)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Deployment!.PlacementSnapshot = WorkloadProtocolTestData.Placement();
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = CreateHealthyWorker(seed.Deployment.PrimaryKey, "game-server", now);
        worker.ProcessId = null;
        worker.ProcessStartedAt = null;
        if (stale)
            worker.LastHeartbeatAt = now - Duration.FromMinutes(5);
        store.DbContext.LifecycleWorkers.Add(worker);
        store.DbContext.WorkerCommands.Add(new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            WorkerFk = worker.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            LifecycleJob = new LifecycleJob
            {
                PrimaryKey = Guid.NewGuid(),
                GameServerFk = seed.Server.PrimaryKey,
                DeploymentFk = seed.Deployment.PrimaryKey,
                RequestedByFk = seed.User.PrimaryKey,
                Kind = ELifecycleJobKind.Restart,
                ActionIdentifier = "restart",
                Sequence = 1,
                Status = ELifecycleJobStatus.Running,
                Stage = "restarting",
                MaxAttempts = 3,
                TimeStampCreated = now,
                TimeStampUpdated = now,
            },
            Sequence = 1,
            ActionIdentifier = "restart",
            Status = EWorkerCommandStatus.Running,
            Stage = "restarting",
            Payload = "{}",
            TimeStampCreated = now,
        });
        await store.DbContext.SaveChangesAsync();
        var reconciler = new DeploymentHealthReconciler(store.DbContext,
            new AutomaticCostStopService(store.DbContext, new HostingAuditWriter()),
            Options.Create(new LifecycleWorkerOptions { StaleThreshold = TimeSpan.FromSeconds(60) }));

        await reconciler.ReconcileAsync();

        Assert.Equal(stale, await store.DbContext.LifecycleJobs.AnyAsync(job => job.Kind == ELifecycleJobKind.Terminate));
        if (stale)
            Assert.Equal("dedicated-worker-heartbeat-stale", seed.Deployment.FaultCode);
    }

    private static LifecycleWorker CreateHealthyWorker(Guid deploymentId, string serviceIdentifier, Instant now)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deploymentId,
            ServiceIdentifier = serviceIdentifier,
            Identity = $"healthy-{serviceIdentifier}",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = now,
            ProcessId = 123,
            ProcessStartedAt = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampRegistered = now,
        };
}
