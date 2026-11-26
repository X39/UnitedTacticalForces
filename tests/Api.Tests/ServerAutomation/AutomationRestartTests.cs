using Api.Tests.GameServerHosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// AUT-06 proof for restarts and repetition: every timer decision is persisted, so a fresh evaluator on a fresh
/// context that shares only the database continues where the previous one stopped and acts exactly once, however many
/// passes or evaluator instances see the same due stop or start.
/// </summary>
public sealed class AutomationRestartTests
{
    /// <summary>The instant the seeded deployment of the maximum runtime scenarios started.</summary>
    private static readonly Instant Started = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>The first reading instant of the idle scenario.</summary>
    private static readonly Instant IdleStart = Instant.FromUnixTimeSeconds(1_700_000_100);

    /// <summary>The calendar day of the schedule scenarios, at midnight UTC.</summary>
    private static readonly Instant Day = Instant.FromUtc(2026, 11, 20, 0, 0);

    /// <summary>The default warning lead of the automation options.</summary>
    private static readonly Duration Lead = Duration.FromMinutes(10);

    /// <summary>Every ISO weekday, so a rule fires on the scenario day whatever weekday it is.</summary>
    private static readonly int[] EveryDay = [1, 2, 3, 4, 5, 6, 7];

    /// <summary>
    /// Gets an instant of the scenario day.
    /// </summary>
    /// <param name="hour">The hour of the day in UTC.</param>
    /// <param name="minute">The minute of the hour.</param>
    /// <returns>The instant.</returns>
    private static Instant At(int hour, int minute) => Day + Duration.FromMinutes(hour * 60 + minute);

    /// <summary>
    /// Creates a context on the shared connection, like a freshly started API process sharing the database.
    /// </summary>
    /// <param name="connection">The open in-memory connection that keeps the database alive.</param>
    /// <returns>A new context; the caller disposes it.</returns>
    private static NodaTimeSqliteApiDbContext CreateDbContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(connection).Options);

    /// <summary>
    /// Runs one evaluator pass on a brand new context and disposes the context afterwards, which is what an API
    /// process that lives for exactly one pass would do.
    /// </summary>
    /// <param name="store">The test store that owns the shared connection.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <returns>A task resolving to the pass result.</returns>
    private static async Task<ServerAutomationEvaluationResult> PassOnFreshProcessAsync(
        HostingTestStore store,
        Instant now)
    {
        await using var context = CreateDbContext(store.Connection);
        return await ServerAutomationTestData.CreateEvaluator(context).EvaluateAsync(now, CancellationToken.None);
    }

    /// <summary>
    /// Seeds a running server with the maximum runtime limit switched on.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <returns>A task resolving to the seed.</returns>
    private static async Task<HostingTestSeed> SeedLimitedServerAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
            q.TimeStampUpdated = Started - Duration.FromDays(1);
        });
        return seed;
    }

    /// <summary>
    /// Seeds a running server whose idle shutdown is switched on with a five minute limit and a live worker.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <returns>A task resolving to the seed.</returns>
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
            IdleStart - Duration.FromSeconds(5),
            IdleStart - Duration.FromSeconds(60));
        return seed;
    }

    /// <summary>Replaces the latest reading and refreshes the heartbeat, like a worker report at the given instant.</summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="seed">The seeded server.</param>
    /// <param name="at">The report instant.</param>
    /// <param name="players">The reported player count.</param>
    /// <returns>A task completing after the reading was saved.</returns>
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

    /// <summary>
    /// A warning persisted before the restart is executed exactly once by a fresh evaluator, and nothing follows.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Restart_WarningPersistedBeforeIt_IsExecutedExactlyOnceAfterIt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var warned = await PassOnFreshProcessAsync(store, warnedAt);
        Assert.Single(warned.RaisedAlerts);
        Assert.Equal(0, warned.JobsQueued);

        await using var contextB = CreateDbContext(store.Connection);
        var evaluatorB = ServerAutomationTestData.CreateEvaluator(contextB);
        var executed = await evaluatorB.EvaluateAsync(warnedAt + Lead, CancellationToken.None);
        var again = await evaluatorB.EvaluateAsync(warnedAt + Lead + Duration.FromMinutes(1), CancellationToken.None);

        Assert.Equal(1, executed.JobsQueued);
        Assert.Equal(0, again.JobsQueued);
        Assert.Single(executed.ResolvedAlerts);
        store.DbContext.ChangeTracker.Clear();
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.MaxRuntime, job.Trigger);
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopStatus.Executed, stop.Status);
        Assert.Equal(job.PrimaryKey, stop.LifecycleJobFk);
        Assert.Single(await store.DbContext.CostAlerts.Where(q => q.Kind == ECostAlertKind.AutoStop).ToArrayAsync());
    }

    /// <summary>
    /// Repeated passes at the same instant after the stop instant, by one evaluator, queue one job, raise one alert and
    /// leave one executed stop.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task DoublePass_SameEvaluatorAtTheSameInstant_QueuesOneJobOneAlertAndOneExecutedStop()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        var stopsAt = warnedAt + Lead;

        var first = await evaluator.EvaluateAsync(stopsAt + Duration.FromSeconds(30), CancellationToken.None);
        var second = await evaluator.EvaluateAsync(stopsAt + Duration.FromSeconds(30), CancellationToken.None);

        Assert.Equal(1, first.JobsQueued);
        Assert.Equal(0, second.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Single(await store.DbContext.CostAlerts.Where(q => q.Kind == ECostAlertKind.AutoStop).ToArrayAsync());
        Assert.Equal(
            EAutoStopStatus.Executed,
            Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync()).Status);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopRequested)
                .ToArrayAsync());
    }

    /// <summary>
    /// Two evaluator instances on separate contexts that both see the same due scheduled stop queue one job, raise one
    /// alert and record one run row.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task DoublePass_TwoEvaluatorsOnSeparateContexts_QueueOneJobOneAlertAndOneRunRow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Deployment!.TimeStampStarted = At(20, 0);
        await store.DbContext.SaveChangesAsync();
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.TimeZone = "UTC");
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Stop,
            EveryDay,
            23 * 60,
            At(19, 0));
        await using var contextA = CreateDbContext(store.Connection);
        await using var contextB = CreateDbContext(store.Connection);
        var evaluatorA = ServerAutomationTestData.CreateEvaluator(contextA);
        var evaluatorB = ServerAutomationTestData.CreateEvaluator(contextB);
        await evaluatorA.EvaluateAsync(At(22, 49), CancellationToken.None);
        var warned = await evaluatorA.EvaluateAsync(At(22, 50), CancellationToken.None);
        Assert.Single(warned.RaisedAlerts);

        var fromA = await evaluatorA.EvaluateAsync(At(23, 0), CancellationToken.None);
        var fromB = await evaluatorB.EvaluateAsync(At(23, 0), CancellationToken.None);
        var fromAAgain = await evaluatorA.EvaluateAsync(At(23, 0), CancellationToken.None);

        Assert.Equal(1, fromA.JobsQueued + fromB.JobsQueued + fromAAgain.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobTrigger.Schedule, job.Trigger);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.Stopped, run.Outcome);
        Assert.Equal(job.PrimaryKey, run.LifecycleJobFk);
        Assert.Single(await store.DbContext.CostAlerts.Where(q => q.Kind == ECostAlertKind.AutoStop).ToArrayAsync());
        Assert.Equal(
            EAutoStopStatus.Executed,
            Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync()).Status);
    }

    /// <summary>
    /// The evaluator's conditional claim affects no row once the stop is executed, so a late or concurrent claim queues
    /// nothing and leaves the recorded resolution alone.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task DirectClaim_OnAnAlreadyExecutedStop_AffectsNoRowAndQueuesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedLimitedServerAsync(store);
        var warnedAt = Started + Duration.FromMinutes(60);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        await evaluator.EvaluateAsync(warnedAt + Lead, CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        var executed = await store.DbContext.AutomationPendingStops.SingleAsync();
        Assert.Equal(EAutoStopStatus.Executed, executed.Status);
        var resolvedAt = executed.ResolvedAt;

        await using var lateProcess = CreateDbContext(store.Connection);
        var stopId = executed.PrimaryKey;
        var late = warnedAt + Lead + Duration.FromMinutes(5);
        var affected = await lateProcess.AutomationPendingStops
            .Where(q => q.PrimaryKey == stopId && q.Status == EAutoStopStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(q => q.Status, EAutoStopStatus.Executed)
                    .SetProperty(q => q.ResolvedAt, late));

        Assert.Equal(0, affected);
        var result = await PassOnFreshProcessAsync(store, late);
        Assert.Equal(0, result.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(resolvedAt, (await store.DbContext.AutomationPendingStops.SingleAsync()).ResolvedAt);
    }

    /// <summary>
    /// The idle clock persisted before the restart continues after it: an unavailable reading right after the restart
    /// holds the clock without stopping or resetting it, and the warning follows once the known idle time, shifted only
    /// by the held time, reaches the limit.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Restart_IdleClockPersistedBeforeIt_ContinuesAndHoldsOnAnUnavailableReading()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedIdleServerAsync(store);
        await ReportAsync(store, seed, IdleStart, 0);
        await PassOnFreshProcessAsync(store, IdleStart);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(IdleStart, (await store.DbContext.ServerAutomationStates.SingleAsync()).IdleSince);

        await using var contextB = CreateDbContext(store.Connection);
        var evaluatorB = ServerAutomationTestData.CreateEvaluator(contextB);
        var held = await evaluatorB.EvaluateAsync(IdleStart + Duration.FromMinutes(1), CancellationToken.None);

        Assert.Empty(held.RaisedAlerts);
        Assert.Equal(0, held.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        var state = await store.DbContext.ServerAutomationStates.SingleAsync();
        Assert.Equal(IdleStart, state.IdleSince);
        Assert.Equal(IdleStart + Duration.FromMinutes(1), state.IdleHeldSince);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Empty(await store.DbContext.CostAlerts.ToArrayAsync());

        // A known zero returns after two held minutes: the idle start shifts forward by exactly the held time.
        foreach (var minutes in new[] { 3, 5, 6 })
        {
            var at = IdleStart + Duration.FromMinutes(minutes);
            await ReportAsync(store, seed, at, 0);
            var early = await evaluatorB.EvaluateAsync(at, CancellationToken.None);
            Assert.Empty(early.RaisedAlerts);
        }

        store.DbContext.ChangeTracker.Clear();
        var shifted = await store.DbContext.ServerAutomationStates.SingleAsync();
        Assert.Equal(IdleStart + Duration.FromMinutes(2), shifted.IdleSince);
        Assert.Null(shifted.IdleHeldSince);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());

        var warnAt = IdleStart + Duration.FromMinutes(7);
        await ReportAsync(store, seed, warnAt, 0);
        var warned = await evaluatorB.EvaluateAsync(warnAt, CancellationToken.None);
        var repeated = await evaluatorB.EvaluateAsync(warnAt, CancellationToken.None);

        Assert.Single(warned.RaisedAlerts);
        Assert.Empty(repeated.RaisedAlerts);
        store.DbContext.ChangeTracker.Clear();
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopReason.Idle, stop.Reason);
        Assert.Equal(5, stop.Limit);
        Assert.Equal(warnAt + Lead, stop.StopsAt);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>
    /// A scheduled stop whose warning window and occurrence both fell into the downtime runs once at the first pass
    /// after the restart and records the downtime in its audit entry.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Restart_ScheduledStopDueWhileDown_RunsOnceAtTheFirstPassAfterIt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Deployment!.TimeStampStarted = At(20, 0);
        await store.DbContext.SaveChangesAsync();
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.TimeZone = "UTC");
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Stop,
            EveryDay,
            23 * 60,
            At(19, 0));
        var before = await PassOnFreshProcessAsync(store, At(22, 0));
        Assert.Empty(before.RaisedAlerts);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(At(22, 0), (await store.DbContext.ServerAutomationStates.SingleAsync()).SchedulesEvaluatedThrough);

        await using var contextB = CreateDbContext(store.Connection);
        var evaluatorB = ServerAutomationTestData.CreateEvaluator(contextB);
        var first = await evaluatorB.EvaluateAsync(At(23, 30), CancellationToken.None);
        var second = await evaluatorB.EvaluateAsync(At(23, 31), CancellationToken.None);

        Assert.Equal(1, first.JobsQueued);
        Assert.Empty(first.RaisedAlerts);
        Assert.Equal(0, second.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.Schedule, job.Trigger);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.Stopped, run.Outcome);
        Assert.Equal(job.PrimaryKey, run.LifecycleJobFk);
        var requested = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopRequested)
                .ToArrayAsync());
        Assert.Contains("came due while the platform was not running", requested.Details);
    }

    /// <summary>
    /// A scheduled start older than the grace window after the downtime is recorded as missed without a job, and a rule
    /// saved after the occurrence records nothing for it.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Restart_ScheduledStartBeyondTheGraceWindow_IsRecordedMissedAndALaterRuleRecordsNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.TimeZone = "UTC";
            q.StartConsentAt = At(10, 0);
            q.StartConsentCostOwnerFk = seed.CostOwner.PrimaryKey;
        });
        seed.CostOwner.Balance = 100m;
        await store.DbContext.SaveChangesAsync();
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Start,
            EveryDay,
            18 * 60,
            At(17, 0));
        var laterRule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Start,
            EveryDay,
            18 * 60 + 5,
            At(18, 30));
        var before = await PassOnFreshProcessAsync(store, At(17, 30));
        Assert.Equal(0, before.JobsQueued);

        await using var contextB = CreateDbContext(store.Connection);
        var evaluatorB = ServerAutomationTestData.CreateEvaluator(contextB);
        var after = await evaluatorB.EvaluateAsync(At(18, 40), CancellationToken.None);
        var repeated = await evaluatorB.EvaluateAsync(At(18, 41), CancellationToken.None);

        Assert.Equal(0, after.JobsQueued);
        Assert.Equal(0, repeated.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(rule.PrimaryKey, run.ScheduleFk);
        Assert.Equal(At(18, 0), run.OccurrenceAt);
        Assert.Equal(EServerScheduleRunOutcome.SkippedMissed, run.Outcome);
        Assert.Null(run.LifecycleJobFk);
        Assert.DoesNotContain(await store.DbContext.ServerScheduleRuns.ToArrayAsync(), q => q.ScheduleFk == laterRule.PrimaryKey);
        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
    }

    /// <summary>
    /// A scheduled start that waits for a stop in flight is stored only in the database: after the stop finished, a
    /// fresh process queues exactly one start, and neither a third process nor a repeated pass queues another.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Restart_ScheduledStartWaitingForAStop_StartsExactlyOnceOnTheNextProcess()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.TimeZone = "UTC";
            q.StartConsentAt = At(10, 0);
            q.StartConsentCostOwnerFk = seed.CostOwner.PrimaryKey;
        });
        seed.CostOwner.Balance = 100m;
        await store.DbContext.SaveChangesAsync();
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Start,
            EveryDay,
            18 * 60,
            At(17, 0));
        await ServerAutomationTestData.AddJobAsync(store.DbContext, seed.Deployment!, ELifecycleJobStatus.Running);

        var recorded = await PassOnFreshProcessAsync(store, At(18, 0));

        Assert.Equal(0, recorded.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        var waiting = await store.DbContext.ServerScheduleRuns.SingleAsync();
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, waiting.Outcome);
        var deployment = await store.DbContext.GameServerDeployments.SingleAsync();
        deployment.Status = EDeploymentStatus.Stopped;
        deployment.TimeStampResolved = At(18, 30);
        (await store.DbContext.LifecycleJobs.SingleAsync()).Status = ELifecycleJobStatus.Succeeded;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var started = await PassOnFreshProcessAsync(store, At(18, 40));
        await using var contextC = CreateDbContext(store.Connection);
        var fromC = await ServerAutomationTestData.CreateEvaluator(contextC)
            .EvaluateAsync(At(18, 40), CancellationToken.None);
        var repeated = await PassOnFreshProcessAsync(store, At(18, 41));

        Assert.Equal(1, started.JobsQueued);
        Assert.Equal(0, fromC.JobsQueued);
        Assert.Equal(0, repeated.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        var start = Assert.Single(
            await store.DbContext.LifecycleJobs.Where(q => q.Kind == ELifecycleJobKind.Start).ToArrayAsync());
        Assert.Equal(ELifecycleJobTrigger.Schedule, start.Trigger);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(waiting.PrimaryKey, run.PrimaryKey);
        Assert.Equal(rule.PrimaryKey, run.ScheduleFk);
        Assert.Equal(EServerScheduleRunOutcome.Started, run.Outcome);
        Assert.Equal(start.PrimaryKey, run.LifecycleJobFk);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledStartRequested)
                .ToArrayAsync());
    }
}
