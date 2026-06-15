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
}
