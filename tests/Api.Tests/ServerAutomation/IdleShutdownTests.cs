using Api.Tests.GameServerHosting;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// AUT-01, AUT-04 and AUT-05 proof for idle shutdown: the evaluator persists the idle clock, warns when the limit is
/// reached, stops one warning lead later, withdraws the warning when players return, and never acts on an
/// unavailable reading.
/// </summary>
public sealed class IdleShutdownTests
{
    private static readonly Instant Start = Instant.FromUnixTimeSeconds(1_700_000_100);
    private static readonly Duration Lead = Duration.FromMinutes(10);

    private static async Task<HostingTestSeed> SeedIdleServerAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.IdleShutdownEnabled = true;
            q.IdleShutdownMinutes = 5;
        });
        await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext,
            seed.Deployment!,
            Start - Duration.FromSeconds(5),
            Start - Duration.FromSeconds(60));
        return seed;
    }

    /// <summary>Replaces the latest reading and refreshes the heartbeat, like a worker report at the given instant.</summary>
    private static async Task ReportAsync(HostingTestStore store, HostingTestSeed seed, Instant at, int players)
    {
        var db = store.DbContext;
        db.ChangeTracker.Clear();
        var worker = await db.LifecycleWorkers.SingleAsync();
        worker.LastHeartbeatAt = at - Duration.FromSeconds(5);
        db.ServerMetricSnapshots.RemoveRange(await db.ServerMetricSnapshots.ToArrayAsync());
        await db.SaveChangesAsync();
        await ServerMetricsTestData.AddSnapshotAsync(
            db,
            seed.Deployment!,
            worker.PrimaryKey,
            at,
            ServerMetricsTestData.Sample(seed.Deployment!.PrimaryKey, at.ToDateTimeOffset(), 10d, 1, 8, players, 32));
    }

    private static async Task<AutomationPendingStop> SinglePendingStopAsync(HostingTestStore store)
    {
        store.DbContext.ChangeTracker.Clear();
        return await store.DbContext.AutomationPendingStops.SingleAsync();
    }

    /// <summary>Zero players at every pass: clock start, warning at the limit, stop one lead later.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task EmptyServer_IsWarnedAtTheLimitAndStoppedOneLeadLater()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        await ReportAsync(store, seed, Start, 0);
        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(Start, (await store.DbContext.ServerAutomationStates.SingleAsync()).IdleSince);

        var warnAt = Start + Duration.FromMinutes(5);
        await ReportAsync(store, seed, Start + Duration.FromMinutes(4), 0);
        var early = await evaluator.EvaluateAsync(Start + Duration.FromMinutes(4), CancellationToken.None);
        Assert.Empty(early.RaisedAlerts);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());

        await ReportAsync(store, seed, warnAt, 0);
        var warned = await evaluator.EvaluateAsync(warnAt, CancellationToken.None);
        var stop = await SinglePendingStopAsync(store);
        Assert.Equal(EAutoStopReason.Idle, stop.Reason);
        Assert.Equal(5, stop.Limit);
        Assert.Equal(warnAt + Lead, stop.StopsAt);
        Assert.Single(warned.RaisedAlerts);

        await ReportAsync(store, seed, warnAt + Lead, 0);
        var stopped = await evaluator.EvaluateAsync(warnAt + Lead, CancellationToken.None);

        Assert.Equal(1, stopped.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        var job = await store.DbContext.LifecycleJobs.SingleAsync();
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.Idle, job.Trigger);
        Assert.Equal(5, job.TriggerLimit);
        Assert.Equal(EAutoStopStatus.Executed, (await store.DbContext.AutomationPendingStops.SingleAsync()).Status);
    }

    /// <summary>Players joining after the warning withdraw it and reset the idle clock.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task PlayersJoiningAfterTheWarning_CancelItAndResetTheClock()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await ReportAsync(store, seed, Start, 0);
        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        var warnAt = Start + Duration.FromMinutes(5);
        await ReportAsync(store, seed, warnAt, 0);
        await evaluator.EvaluateAsync(warnAt, CancellationToken.None);

        var joinAt = warnAt + Duration.FromMinutes(2);
        await ReportAsync(store, seed, joinAt, 2);
        var result = await evaluator.EvaluateAsync(joinAt, CancellationToken.None);

        var stop = await SinglePendingStopAsync(store);
        Assert.Equal(EAutoStopStatus.Cancelled, stop.Status);
        Assert.Equal("activity", stop.CancelReason);
        Assert.Single(result.ResolvedAlerts);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopCancelled)
                .ToArrayAsync());
        Assert.Null((await store.DbContext.ServerAutomationStates.SingleAsync()).IdleSince);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>
    /// AUT-01: a stale reading after the warning neither stops nor cancels; a known zero after the stop instant then
    /// queues the stop.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task UnavailableReadingAfterTheWarning_NeitherStopsNorCancels()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await ReportAsync(store, seed, Start, 0);
        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        var warnAt = Start + Duration.FromMinutes(5);
        await ReportAsync(store, seed, warnAt, 0);
        await evaluator.EvaluateAsync(warnAt, CancellationToken.None);

        // No new report: the reading is stale when the stop is due.
        var held = await evaluator.EvaluateAsync(warnAt + Lead + Duration.FromMinutes(1), CancellationToken.None);

        Assert.Equal(0, held.JobsQueued);
        Assert.Empty(held.ResolvedAlerts);
        Assert.Equal(EAutoStopStatus.Pending, (await SinglePendingStopAsync(store)).Status);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());

        var knownAt = warnAt + Lead + Duration.FromMinutes(2);
        await ReportAsync(store, seed, knownAt, 0);
        var stopped = await evaluator.EvaluateAsync(knownAt, CancellationToken.None);

        Assert.Equal(1, stopped.JobsQueued);
    }

    /// <summary>A server without any reading never starts an idle clock or a warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task ServerWithoutAnyReading_NeverStartsAClockOrWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedIdleServerAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        var later = await evaluator.EvaluateAsync(Start + Duration.FromHours(5), CancellationToken.None);

        Assert.Empty(later.RaisedAlerts);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Empty(await store.DbContext.ServerAutomationStates.Where(q => q.IdleSince != null).ToArrayAsync());
    }

    /// <summary>A game that does not report players never arms idle shutdown, even with the setting on.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task GameWithoutPlayerCount_NeverArmsIdleShutdown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext, reportsPlayerCount: false);
        await ReportAsync(store, seed, Start, 0);

        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        var later = await evaluator.EvaluateAsync(Start + Duration.FromHours(1), CancellationToken.None);

        Assert.Empty(later.RaisedAlerts);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Empty(await store.DbContext.ServerAutomationStates.ToArrayAsync());
    }

    /// <summary>A queued lifecycle job of the deployment counts as activity: no clock, no warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task QueuedLifecycleJob_CountsAsActivity()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        await ServerAutomationTestData.AddJobAsync(
            store.DbContext,
            seed.Deployment!,
            ELifecycleJobStatus.Queued,
            ELifecycleJobKind.Restart);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        await ReportAsync(store, seed, Start, 0);
        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        await ReportAsync(store, seed, Start + Duration.FromMinutes(30), 0);
        var later = await evaluator.EvaluateAsync(Start + Duration.FromMinutes(30), CancellationToken.None);

        Assert.Empty(later.RaisedAlerts);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
    }

    /// <summary>The evaluator updates the clock row and never changes the settings version or save time.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_UpdatesTheStateRowAndLeavesTheSettingsVersionAlone()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        var before = await store.DbContext.ServerAutomationSettings.AsNoTracking().SingleAsync();
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await ReportAsync(store, seed, Start, 0);

        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        await ReportAsync(store, seed, Start + Duration.FromMinutes(5), 0);
        await evaluator.EvaluateAsync(Start + Duration.FromMinutes(5), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var state = await store.DbContext.ServerAutomationStates.SingleAsync();
        Assert.Equal(seed.Deployment!.PrimaryKey, state.IdleDeploymentFk);
        Assert.Equal(Start, state.IdleSince);
        var after = await store.DbContext.ServerAutomationSettings.AsNoTracking().SingleAsync();
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.TimeStampUpdated, after.TimeStampUpdated);
    }

    /// <summary>Switching idle shutdown off withdraws an open warning as a settings change.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task SwitchingIdleShutdownOff_CancelsTheWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await ReportAsync(store, seed, Start, 0);
        await evaluator.EvaluateAsync(Start, CancellationToken.None);
        var warnAt = Start + Duration.FromMinutes(5);
        await ReportAsync(store, seed, warnAt, 0);
        await evaluator.EvaluateAsync(warnAt, CancellationToken.None);
        var settings = await store.DbContext.ServerAutomationSettings.SingleAsync();
        settings.IdleShutdownEnabled = false;
        await store.DbContext.SaveChangesAsync();

        var result = await evaluator.EvaluateAsync(warnAt + Duration.FromMinutes(1), CancellationToken.None);

        var stop = await SinglePendingStopAsync(store);
        Assert.Equal(EAutoStopStatus.Cancelled, stop.Status);
        Assert.Equal("settings-changed", stop.CancelReason);
        Assert.Single(result.ResolvedAlerts);
        Assert.Null((await store.DbContext.ServerAutomationStates.SingleAsync()).IdleSince);
    }
}
