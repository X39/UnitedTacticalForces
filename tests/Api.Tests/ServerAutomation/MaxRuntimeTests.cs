using Api.Tests.GameServerHosting;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// AUT-02, AUT-04, AUT-05 and AUT-06 proof for the maximum runtime limit: the owner is warned when the limit is
/// reached, an ordinary stop job follows one warning lead later exactly once, a changed limit withdraws the warning,
/// and a limit that came due while the platform was down stops the server without a warning.
/// </summary>
public sealed class MaxRuntimeTests
{
    /// <summary>The instant the seeded deployment started.</summary>
    private static readonly Instant Started = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>The default warning lead of the automation options.</summary>
    private static readonly Duration Lead = Duration.FromMinutes(10);

    private static async Task<HostingTestSeed> SeedLimitedServerAsync(
        HostingTestStore store,
        int hours = 1,
        Instant? savedAt = null)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = hours;
            q.TimeStampUpdated = savedAt ?? Started - Duration.FromDays(1);
        });
        return seed;
    }

    /// <summary>One minute before the limit nothing is recorded.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_BeforeTheLimit_CreatesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);

        var result = await ServerAutomationTestData.CreateEvaluator(store.DbContext)
            .EvaluateAsync(Started + Duration.FromMinutes(59), CancellationToken.None);

        Assert.Empty(result.RaisedAlerts);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Empty(await store.DbContext.CostAlerts.ToArrayAsync());
    }

    /// <summary>At the limit one pending stop, one alert with deliveries and one audit entry are committed together.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_AtTheLimit_WarnsWithPendingStopAlertAndAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedLimitedServerAsync(store);
        var now = Started + Duration.FromMinutes(60);

        var result = await ServerAutomationTestData.CreateEvaluator(store.DbContext)
            .EvaluateAsync(now, CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopReason.MaxRuntime, stop.Reason);
        Assert.Equal(1, stop.Limit);
        Assert.Equal(EAutoStopStatus.Pending, stop.Status);
        Assert.Equal(now, stop.WarnedAt);
        Assert.Equal(now + Lead, stop.StopsAt);
        Assert.Equal(seed.Deployment!.PrimaryKey, stop.DeploymentFk);
        var alert = Assert.Single(await store.DbContext.CostAlerts.Include(q => q.Deliveries).ToArrayAsync());
        Assert.Equal(ECostAlertKind.AutoStop, alert.Kind);
        Assert.Equal(stop.PrimaryKey, alert.PendingStopFk);
        Assert.NotEmpty(alert.Deliveries);
        var audit = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopWarned)
                .ToArrayAsync());
        Assert.Null(audit.ActorFk);
        Assert.Equal(ECostImpact.NoCostImpact, audit.CostImpact);
        var raised = Assert.Single(result.RaisedAlerts);
        Assert.Equal(alert.PrimaryKey, raised.CostAlertId);
        Assert.Equal(now + Lead, raised.StopsAt);
        Assert.Equal(EAutoStopReason.MaxRuntime, raised.StopReason);
        Assert.Equal(0, result.JobsQueued);
    }

    /// <summary>A repeated pass at the same instant records no second warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_RepeatedAtTheSameInstant_CreatesNothingNew()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);
        var now = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(now, CancellationToken.None);

        var second = await evaluator.EvaluateAsync(now, CancellationToken.None);

        Assert.Empty(second.RaisedAlerts);
        Assert.Empty(second.ResolvedAlerts);
        Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Single(await store.DbContext.CostAlerts.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>At the stop instant the claim and one ordinary stop job commit and the alert is resolved.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_AtTheStopInstant_ClaimsTheStopQueuesOneStopJobAndResolvesTheAlert()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        var warned = await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);

        var result = await evaluator.EvaluateAsync(warnedAt + Lead, CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.MaxRuntime, job.Trigger);
        Assert.Equal(1, job.TriggerLimit);
        Assert.Null(job.RequestedByFk);
        Assert.Equal(50, job.Priority);
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopStatus.Executed, stop.Status);
        Assert.Equal(job.PrimaryKey, stop.LifecycleJobFk);
        Assert.Equal(warnedAt + Lead, stop.ResolvedAt);
        var requested = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopRequested)
                .ToArrayAsync());
        Assert.Equal(job.PrimaryKey, requested.LifecycleJobFk);
        var deliveries = await store.DbContext.CostAlertDeliveries.ToArrayAsync();
        Assert.DoesNotContain(deliveries, q => q.Status == ECostAlertDeliveryStatus.Pending);
        var resolved = Assert.Single(result.ResolvedAlerts);
        Assert.Equal(Assert.Single(warned.RaisedAlerts).CostAlertId, resolved.CostAlertId);
        Assert.Equal(1, result.JobsQueued);

        var again = await evaluator.EvaluateAsync(warnedAt + Lead + Duration.FromMinutes(1), CancellationToken.None);
        Assert.Equal(0, again.JobsQueued);
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>Raising the limit before the stop withdraws the warning and queues nothing.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_AfterTheLimitWasRaised_CancelsTheWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        var settings = await store.DbContext.ServerAutomationSettings
            .SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey);
        settings.MaxRuntimeHours = 3;
        settings.TimeStampUpdated = warnedAt + Duration.FromMinutes(2);
        await store.DbContext.SaveChangesAsync();

        var result = await evaluator.EvaluateAsync(warnedAt + Duration.FromMinutes(5), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopStatus.Cancelled, stop.Status);
        Assert.Equal("settings-changed", stop.CancelReason);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopCancelled)
                .ToArrayAsync());
        Assert.Single(result.ResolvedAlerts);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>Enabling the limit on a server that already ran longer warns first and never stops at once.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_LimitEnabledOnLongRunningServer_WarnsThenStopsOneLeadLater()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var enabledAt = Started + Duration.FromMinutes(70);
        await SeedLimitedServerAsync(store, savedAt: enabledAt);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var warned = await evaluator.EvaluateAsync(enabledAt + Duration.FromSeconds(15), CancellationToken.None);
        var early = await evaluator.EvaluateAsync(enabledAt + Duration.FromMinutes(5), CancellationToken.None);
        var stopped = await evaluator.EvaluateAsync(
            enabledAt + Duration.FromSeconds(15) + Lead,
            CancellationToken.None);

        Assert.Single(warned.RaisedAlerts);
        Assert.Equal(0, warned.JobsQueued);
        Assert.Equal(0, early.JobsQueued);
        Assert.Equal(1, stopped.JobsQueued);
    }

    /// <summary>A limit that came due while the platform was down stops at once, and the audit says so.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_LimitCameDueWhileDown_QueuesTheStopWithoutWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);
        var now = Started + Duration.FromMinutes(60) + Lead;

        var result = await ServerAutomationTestData.CreateEvaluator(store.DbContext)
            .EvaluateAsync(now, CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Null(stop.WarnedAt);
        Assert.Equal(EAutoStopStatus.Executed, stop.Status);
        Assert.NotNull(stop.LifecycleJobFk);
        var requested = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopRequested)
                .ToArrayAsync());
        Assert.Contains("came due while the platform was not running", requested.Details);
        Assert.Empty(await store.DbContext.CostAlerts.ToArrayAsync());
        Assert.Empty(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopWarned)
            .ToArrayAsync());
        Assert.Equal(1, result.JobsQueued);
    }

    /// <summary>
    /// Another pending lifecycle action leaves the stop pending; once it finished the next pass queues the stop.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_WhenAnotherActionIsPending_KeepsTheStopPendingAndRetriesLater()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        var restart = await ServerAutomationTestData.AddJobAsync(
            store.DbContext,
            seed.Deployment!,
            ELifecycleJobStatus.Running,
            ELifecycleJobKind.Restart);

        var blocked = await evaluator.EvaluateAsync(warnedAt + Lead, CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(0, blocked.JobsQueued);
        Assert.Equal(
            EAutoStopStatus.Pending,
            (await store.DbContext.AutomationPendingStops.SingleAsync()).Status);
        Assert.Equal(restart.PrimaryKey, (await store.DbContext.LifecycleJobs.SingleAsync()).PrimaryKey);

        var finished = await store.DbContext.LifecycleJobs.SingleAsync();
        finished.Status = ELifecycleJobStatus.Succeeded;
        await store.DbContext.SaveChangesAsync();
        var retried = await evaluator.EvaluateAsync(warnedAt + Lead + Duration.FromSeconds(15), CancellationToken.None);

        Assert.Equal(1, retried.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Contains(
            await store.DbContext.LifecycleJobs.ToArrayAsync(),
            q => q.Kind == ELifecycleJobKind.Stop && q.Trigger == ELifecycleJobTrigger.MaxRuntime);
    }

    /// <summary>
    /// A failed automatic stop is never escalated: no terminate job and no second stop job is queued while the
    /// failed one waits for a person.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_AfterAFailedStop_NeverQueuesATerminateJobOrAnotherStop()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        await evaluator.EvaluateAsync(warnedAt + Lead, CancellationToken.None);
        var job = await store.DbContext.LifecycleJobs.SingleAsync();
        job.Status = ELifecycleJobStatus.Failed;
        await store.DbContext.SaveChangesAsync();

        await evaluator.EvaluateAsync(warnedAt + Lead + Duration.FromHours(1), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.DoesNotContain(
            await store.DbContext.LifecycleJobs.ToArrayAsync(),
            q => q.Kind == ELifecycleJobKind.Terminate);
        Assert.Equal(seed.Deployment!.PrimaryKey, (await store.DbContext.LifecycleJobs.SingleAsync()).DeploymentFk);
    }

    /// <summary>A deployment that is no longer running supersedes its open warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_WhenTheDeploymentStoppedRunning_SupersedesTheWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        var deployment = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == seed.Deployment!.PrimaryKey);
        deployment.Status = EDeploymentStatus.Stopping;
        await store.DbContext.SaveChangesAsync();

        var result = await evaluator.EvaluateAsync(warnedAt + Duration.FromMinutes(1), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stop = await store.DbContext.AutomationPendingStops.SingleAsync();
        Assert.Equal(EAutoStopStatus.Superseded, stop.Status);
        Assert.Equal("deployment-not-running", stop.CancelReason);
        Assert.Single(result.ResolvedAlerts);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }
}
