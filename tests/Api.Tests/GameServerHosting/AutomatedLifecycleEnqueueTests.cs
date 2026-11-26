using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the control plane can queue an ordinary start or stop job without a user, that it can never queue
/// anything else, and that the manual request path is unchanged by the shared enqueue core.
/// </summary>
public sealed class AutomatedLifecycleEnqueueTests
{
    private static readonly Instant EvaluatedAt = Instant.FromUnixTimeSeconds(1_700_000_500);

    /// <summary>
    /// An automatic stop is a normal stop job with its trigger and limit, queued without a user.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStop_QueuesNormalStopJobWithTriggerAndAutomationAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        var job = await service.EnqueueAutomatedActionAsync(new AutomatedLifecycleRequest(
            seed.Server.PrimaryKey,
            GameServerActions.Stop,
            ELifecycleJobTrigger.Idle,
            30,
            EvaluatedAt));

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == job.PrimaryKey);
        Assert.Equal(ELifecycleJobKind.Stop, persisted.Kind);
        Assert.Equal(50, persisted.Priority);
        Assert.Null(persisted.RequestedByFk);
        Assert.Equal(ELifecycleJobTrigger.Idle, persisted.Trigger);
        Assert.Equal(30, persisted.TriggerLimit);
        Assert.Equal(ELifecycleJobStatus.Queued, persisted.Status);
        Assert.Equal(EvaluatedAt, persisted.TimeStampCreated);
        Assert.True(
            await store.DbContext.GameServerDeployments
                .Where(q => q.PrimaryKey == seed.Deployment!.PrimaryKey)
                .Select(q => q.IsProvisioningBlocked)
                .SingleAsync());
        var audits = await store.DbContext.HostingAuditRecords.ToArrayAsync();
        var queued = Assert.Single(audits, q => q.LifecycleJobFk == job.PrimaryKey);
        Assert.Equal(EHostingAuditEventKind.AutomaticStopRequested, queued.Kind);
        Assert.Null(queued.ActorFk);
        Assert.Equal(ECostImpact.StopsCost, queued.CostImpact);
        Assert.Contains("Trigger=Idle", queued.Details);
        Assert.Contains("Limit=30", queued.Details);
        Assert.DoesNotContain(audits, q => q.Kind == EHostingAuditEventKind.LifecycleJobQueued);
    }

    /// <summary>
    /// An automatic stop skips the confirmation text that a manual stop of the same game requires.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStop_DoesNotNeedTheManualConfirmationTitle()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        var job = await service.EnqueueAutomatedActionAsync(new AutomatedLifecycleRequest(
            seed.Server.PrimaryKey,
            "  STOP ",
            ELifecycleJobTrigger.MaxRuntime,
            4,
            EvaluatedAt));

        Assert.Equal(GameServerActions.Stop, job.ActionIdentifier);
        Assert.Equal(ELifecycleJobTrigger.MaxRuntime, job.Trigger);
    }

    /// <summary>
    /// A request that cannot be queued leaves no job and no audit row behind.
    /// </summary>
    /// <param name="status">The deployment status the server is seeded with.</param>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Theory]
    [InlineData(EDeploymentStatus.Provisioning)]
    [InlineData(EDeploymentStatus.Stopping)]
    public async Task AutomatedStop_WhenDeploymentIsNotRunning_ConflictsAndWritesNothing(EDeploymentStatus status)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: status);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.EnqueueAutomatedActionAsync(
            new AutomatedLifecycleRequest(
                seed.Server.PrimaryKey,
                GameServerActions.Stop,
                ELifecycleJobTrigger.Idle,
                30,
                EvaluatedAt)));

        store.DbContext.ChangeTracker.Clear();
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    /// <summary>
    /// A second lifecycle action cannot be queued while another one is still pending.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStop_WhenAnotherActionIsPending_ConflictsAndWritesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var pending = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Restart,
            ActionIdentifier = GameServerActions.Restart,
            Sequence = 1,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            MaxAttempts = 3,
            TimeStampCreated = EvaluatedAt,
            TimeStampUpdated = EvaluatedAt,
        };
        store.DbContext.LifecycleJobs.Add(pending);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.EnqueueAutomatedActionAsync(
            new AutomatedLifecycleRequest(
                seed.Server.PrimaryKey,
                GameServerActions.Stop,
                ELifecycleJobTrigger.Idle,
                30,
                EvaluatedAt)));

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(pending.PrimaryKey, (await store.DbContext.LifecycleJobs.SingleAsync()).PrimaryKey);
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    /// <summary>
    /// The job is visible inside the caller's transaction and disappears with it when the caller rolls back.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStop_JoinsCallerTransaction_RollbackRemovesJobAndAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await using (var callerTransaction = await store.DbContext.Database.BeginTransactionAsync())
        {
            var job = await service.EnqueueAutomatedActionAsync(new AutomatedLifecycleRequest(
                seed.Server.PrimaryKey,
                GameServerActions.Stop,
                ELifecycleJobTrigger.Schedule,
                null,
                EvaluatedAt));

            Assert.NotNull(store.DbContext.Database.CurrentTransaction);
            Assert.Equal(job.PrimaryKey, (await store.DbContext.LifecycleJobs.SingleAsync()).PrimaryKey);
            Assert.Single(await store.DbContext.HostingAuditRecords.ToArrayAsync());

            await callerTransaction.RollbackAsync();
        }

        store.DbContext.ChangeTracker.Clear();
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
        Assert.False(
            await store.DbContext.GameServerDeployments
                .Where(q => q.PrimaryKey == seed.Deployment!.PrimaryKey)
                .Select(q => q.IsProvisioningBlocked)
                .SingleAsync());
    }

    /// <summary>
    /// The core does not commit a transaction it did not open, so the caller decides the outcome.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStop_JoinsCallerTransaction_CallerCommitKeepsJobAndAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await using (var callerTransaction = await store.DbContext.Database.BeginTransactionAsync())
        {
            await service.EnqueueAutomatedActionAsync(new AutomatedLifecycleRequest(
                seed.Server.PrimaryKey,
                GameServerActions.Stop,
                ELifecycleJobTrigger.Schedule,
                null,
                EvaluatedAt));

            Assert.NotNull(store.DbContext.Database.CurrentTransaction);
            await callerTransaction.CommitAsync();
        }

        store.DbContext.ChangeTracker.Clear();
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Single(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    /// <summary>
    /// Terminate and every other action, and the manual trigger, are refused so no automatic path creates a terminate job.
    /// </summary>
    /// <param name="actionId">The action the automated request names.</param>
    /// <param name="trigger">The trigger the automated request carries.</param>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Theory]
    [InlineData(GameServerActions.Terminate, ELifecycleJobTrigger.Idle)]
    [InlineData(GameServerActions.Restart, ELifecycleJobTrigger.Idle)]
    [InlineData(GameServerActions.Update, ELifecycleJobTrigger.Schedule)]
    [InlineData(GameServerActions.CollectLogs, ELifecycleJobTrigger.MaxRuntime)]
    [InlineData("anything-custom", ELifecycleJobTrigger.Idle)]
    [InlineData(GameServerActions.Stop, ELifecycleJobTrigger.Manual)]
    [InlineData(GameServerActions.Start, ELifecycleJobTrigger.Manual)]
    public async Task AutomatedRequest_ForUnsupportedActionOrManualTrigger_ThrowsValidationAndWritesNothing(
        string actionId,
        ELifecycleJobTrigger trigger)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingValidationException>(() => service.EnqueueAutomatedActionAsync(
            new AutomatedLifecycleRequest(seed.Server.PrimaryKey, actionId, trigger, null, EvaluatedAt)));

        store.DbContext.ChangeTracker.Clear();
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
        Assert.False(
            await store.DbContext.GameServerDeployments
                .Where(q => q.PrimaryKey == seed.Deployment!.PrimaryKey)
                .Select(q => q.IsProvisioningBlocked)
                .SingleAsync());
    }

    /// <summary>
    /// An archived or missing server is reported as not found.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStop_ForUnknownServer_ThrowsNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingNotFoundException>(() => service.EnqueueAutomatedActionAsync(
            new AutomatedLifecycleRequest(
                Guid.NewGuid(),
                GameServerActions.Stop,
                ELifecycleJobTrigger.Idle,
                30,
                EvaluatedAt)));
    }

    /// <summary>
    /// A scheduled start creates its deployment and a start job through the same path as a manual start.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStart_QueuesStartJobWithScheduledStartAudit_AndAppendsTheNote()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        var job = await service.EnqueueAutomatedActionAsync(new AutomatedLifecycleRequest(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            ELifecycleJobTrigger.Schedule,
            null,
            EvaluatedAt,
            "came due while the platform was not running"));

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == job.PrimaryKey);
        Assert.Equal(ELifecycleJobKind.Start, persisted.Kind);
        Assert.Equal(0, persisted.Priority);
        Assert.Null(persisted.RequestedByFk);
        Assert.Equal(ELifecycleJobTrigger.Schedule, persisted.Trigger);
        Assert.Null(persisted.TriggerLimit);
        Assert.Equal(EvaluatedAt, persisted.TimeStampCreated);
        var audits = await store.DbContext.HostingAuditRecords.ToArrayAsync();
        var queued = Assert.Single(audits, q => q.LifecycleJobFk == job.PrimaryKey);
        Assert.Equal(EHostingAuditEventKind.ScheduledStartRequested, queued.Kind);
        Assert.Null(queued.ActorFk);
        Assert.Equal(ECostImpact.StartsCost, queued.CostImpact);
        Assert.Contains("Trigger=Schedule", queued.Details);
        Assert.Contains("came due while the platform was not running", queued.Details);
        Assert.Contains(audits, q => q.Kind == EHostingAuditEventKind.DeploymentCreated && q.ActorFk is null);
    }

    /// <summary>
    /// A scheduled start still refuses a frozen cost owner, like a manual start does.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task AutomatedStart_WithFrozenCostOwner_ConflictsAndWritesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.IsFrozen = true;
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.EnqueueAutomatedActionAsync(
            new AutomatedLifecycleRequest(
                seed.Server.PrimaryKey,
                GameServerActions.Start,
                ELifecycleJobTrigger.Schedule,
                null,
                EvaluatedAt)));

        store.DbContext.ChangeTracker.Clear();
        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>
    /// The manual path keeps its trigger, its requesting user and its audit kind.
    /// </summary>
    /// <returns>A task that completes when the assertions finished.</returns>
    [Fact]
    public async Task ManualStop_StillCreatesManualJobWithLifecycleJobQueuedAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);

        var job = await service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Stop,
            seed.Server.Title,
            principal);

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == job.PrimaryKey);
        Assert.Equal(ELifecycleJobTrigger.Manual, persisted.Trigger);
        Assert.Null(persisted.TriggerLimit);
        Assert.Equal(seed.User.PrimaryKey, persisted.RequestedByFk);
        var queued = Assert.Single(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.LifecycleJobFk == job.PrimaryKey);
        Assert.Equal(EHostingAuditEventKind.LifecycleJobQueued, queued.Kind);
        Assert.Equal(seed.User.PrimaryKey, queued.ActorFk);
    }
}
