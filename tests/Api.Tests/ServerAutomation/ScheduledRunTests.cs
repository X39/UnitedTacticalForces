using Api.Tests.GameServerHosting;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// AUT-03, AUT-04, AUT-05 and AUT-06 proof for weekly schedule rules: a stop rule warns one lead before its time and
/// stops the server at its time exactly once, catches up safely after downtime, and competes correctly with the other
/// automatic stops; a start rule starts the server only with consent and an owner who can pay and records every skip.
/// </summary>
public sealed class ScheduledRunTests
{
    private static readonly Instant Day = Instant.FromUtc(2026, 11, 20, 0, 0);
    private static readonly Duration Lead = Duration.FromMinutes(10);
    private static readonly int[] EveryDay = [1, 2, 3, 4, 5, 6, 7];

    private static Instant At(int hour, int minute, int second = 0)
        => Day + Duration.FromSeconds(hour * 3600 + minute * 60 + second);

    private static int Minute(int hour, int minute) => hour * 60 + minute;

    private static async Task<(HostingTestSeed Seed, ServerAutomationSettings Settings)> SeedAsync(
        HostingTestStore store,
        bool withDeployment = true,
        Instant? startedAt = null,
        Action<ServerAutomationSettings>? configure = null)
    {
        var db = store.DbContext;
        var seed = await HostingTestData.SeedServerAsync(db, withActiveDeployment: withDeployment);
        if (seed.Deployment is { } deployment)
        {
            deployment.TimeStampStarted = startedAt ?? At(20, 0);
            await db.SaveChangesAsync();
        }

        var settings = await ServerAutomationTestData.SeedSettingsAsync(db, seed.Server, q =>
        {
            q.TimeZone = "UTC";
            configure?.Invoke(q);
        });
        return (seed, settings);
    }

    private static Task<ServerSchedule> AddStopRuleAsync(
        HostingTestStore store,
        ServerAutomationSettings settings,
        int hour,
        int minute,
        Instant? effectiveFrom = null)
        => ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Stop,
            EveryDay,
            Minute(hour, minute),
            effectiveFrom ?? At(19, 0));

    /// <summary>One lead before the stop time nothing happens, at the window start a warning is recorded.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StopRule_WarnsOneLeadBeforeItsTime()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store);
        var rule = await AddStopRuleAsync(store, settings, 23, 0);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var early = await evaluator.EvaluateAsync(At(22, 49), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        Assert.Empty(early.RaisedAlerts);
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());

        var warned = await evaluator.EvaluateAsync(At(22, 50), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopReason.Schedule, stop.Reason);
        Assert.Equal(rule.PrimaryKey, stop.ScheduleFk);
        Assert.Equal(At(23, 0), stop.ScheduleOccurrenceAt);
        Assert.Equal(At(23, 0), stop.StopsAt);
        Assert.Equal(At(22, 50), stop.WarnedAt);
        Assert.Equal(seed.Deployment!.PrimaryKey, stop.DeploymentFk);
        Assert.Single(warned.RaisedAlerts);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopWarned)
                .ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>At the stop time one ordinary stop job with the schedule trigger is queued and the run is recorded.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StopRule_AtItsTime_QueuesOneStopJobAndRecordsTheRun()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, settings) = await SeedAsync(store);
        var rule = await AddStopRuleAsync(store, settings, 23, 0);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(At(22, 49), CancellationToken.None);
        var warned = await evaluator.EvaluateAsync(At(22, 50), CancellationToken.None);

        var result = await evaluator.EvaluateAsync(At(23, 0), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.Schedule, job.Trigger);
        Assert.Null(job.RequestedByFk);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(rule.PrimaryKey, run.ScheduleFk);
        Assert.Equal(At(23, 0), run.OccurrenceAt);
        Assert.Equal(EServerScheduleRunOutcome.Stopped, run.Outcome);
        Assert.Equal(job.PrimaryKey, run.LifecycleJobFk);
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopStatus.Executed, stop.Status);
        Assert.Equal(job.PrimaryKey, stop.LifecycleJobFk);
        Assert.Equal(1, result.JobsQueued);
        Assert.Equal(Assert.Single(warned.RaisedAlerts).CostAlertId, Assert.Single(result.ResolvedAlerts).CostAlertId);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopRequested)
                .ToArrayAsync());
    }

    /// <summary>Removing the rule before its time withdraws the warning and records no run.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task DeletingTheRule_CancelsTheWarningAndRecordsNoRun()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, settings) = await SeedAsync(store);
        var rule = await AddStopRuleAsync(store, settings, 23, 0);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(At(22, 49), CancellationToken.None);
        await evaluator.EvaluateAsync(At(22, 50), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        store.DbContext.ServerSchedules.Remove(await store.DbContext.ServerSchedules.SingleAsync());
        await store.DbContext.SaveChangesAsync();

        var result = await evaluator.EvaluateAsync(At(22, 55), CancellationToken.None);
        await evaluator.EvaluateAsync(At(23, 0), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopStatus.Cancelled, stop.Status);
        Assert.Equal(PendingStopPlanner.SettingsChangedReason, stop.CancelReason);
        Assert.Single(result.ResolvedAlerts);
        Assert.Empty(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.NotEqual(Guid.Empty, rule.PrimaryKey);
    }

    /// <summary>Changing the rule's time resets its effective instant and withdraws the old warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task ChangingTheRuleTime_CancelsTheOldWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (_, settings) = await SeedAsync(store);
        await AddStopRuleAsync(store, settings, 23, 0);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await evaluator.EvaluateAsync(At(22, 49), CancellationToken.None);
        await evaluator.EvaluateAsync(At(22, 50), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        var stored = await store.DbContext.ServerSchedules.SingleAsync();
        stored.MinuteOfDay = Minute(23, 5);
        stored.EffectiveFrom = At(22, 52);
        await store.DbContext.SaveChangesAsync();

        await evaluator.EvaluateAsync(At(22, 55), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var old = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(EAutoStopStatus.Cancelled, old.Status);

        await evaluator.EvaluateAsync(At(22, 55, 15), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stops = await store.DbContext.AutomationPendingStops.OrderBy(q => q.TimeStampCreated).ToArrayAsync();
        Assert.Equal(2, stops.Length);
        Assert.Equal(EAutoStopStatus.Pending, stops[1].Status);
        Assert.Equal(At(23, 5), stops[1].StopsAt);
        Assert.Empty(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
    }

    /// <summary>A stop that came due during downtime runs once at the first pass, without a warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StopDueDuringDowntime_RunsOnceAtTheFirstPassWithTheDowntimeNote()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store);
        await AddStopRuleAsync(store, settings, 23, 0);
        await ServerAutomationTestData.SeedWatermarkAsync(store.DbContext, seed.Server, At(22, 40));
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var result = await evaluator.EvaluateAsync(At(23, 30), CancellationToken.None);
        await evaluator.EvaluateAsync(At(23, 31), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, result.JobsQueued);
        Assert.Empty(result.RaisedAlerts);
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobTrigger.Schedule, job.Trigger);
        var stop = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Null(stop.WarnedAt);
        Assert.Equal(EAutoStopStatus.Executed, stop.Status);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.Stopped, run.Outcome);
        var requested = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.AutomaticStopRequested)
                .ToArrayAsync());
        Assert.Contains("came due while the platform was not running", requested.Details);
        Assert.Equal(At(23, 30), (await store.DbContext.ServerAutomationStates.SingleAsync()).SchedulesEvaluatedThrough);
    }

    /// <summary>A server started after the stop time during downtime is not stopped for the missed occurrence.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StopDueDuringDowntime_ServerStartedLater_IsRecordedMissed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store, startedAt: At(23, 10));
        await AddStopRuleAsync(store, settings, 23, 0);
        await ServerAutomationTestData.SeedWatermarkAsync(store.DbContext, seed.Server, At(22, 40));

        var result = await ServerAutomationTestData.CreateEvaluator(store.DbContext)
            .EvaluateAsync(At(23, 30), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(0, result.JobsQueued);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.SkippedMissed, run.Outcome);
        var skipped = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped)
                .ToArrayAsync());
        Assert.Contains("started after", skipped.Summary);
    }

    /// <summary>A stop due while no deployment runs is recorded as off and nothing is queued.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StopDue_WithoutARunningDeployment_IsRecordedOff()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store, withDeployment: false);
        await AddStopRuleAsync(store, settings, 23, 0);
        await ServerAutomationTestData.SeedWatermarkAsync(store.DbContext, seed.Server, At(22, 40));

        await ServerAutomationTestData.CreateEvaluator(store.DbContext)
            .EvaluateAsync(At(23, 30), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.SkippedOff, run.Outcome);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped)
                .ToArrayAsync());
    }

    /// <summary>Occurrences before the rule's effective instant or the watermark are never acted on.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task SavingTheRule_NeverFiresThePast()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store);
        await AddStopRuleAsync(store, settings, 23, 0, effectiveFrom: At(23, 20));
        await ServerAutomationTestData.SeedWatermarkAsync(store.DbContext, seed.Server, At(22, 40));

        var result = await ServerAutomationTestData.CreateEvaluator(store.DbContext)
            .EvaluateAsync(At(23, 30), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(0, result.JobsQueued);
        Assert.Empty(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Empty(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>A scheduled stop whose window opens before another warning's stop supersedes that warning.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task EarlierScheduledStop_SupersedesTheOpenMaximumRuntimeWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store, configure: q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
        });
        var rule = await AddStopRuleAsync(store, settings, 23, 0);
        var limit = await ServerAutomationTestData.AddPendingStopAsync(
            store.DbContext,
            seed.Deployment!,
            EAutoStopReason.MaxRuntime,
            At(23, 5),
            1);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var result = await evaluator.EvaluateAsync(At(22, 50), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stops = await store.DbContext.AutomationPendingStops.OrderByDescending(q => q.StopsAt).ToArrayAsync();
        Assert.Equal(2, stops.Length);
        Assert.Equal(limit.PrimaryKey, stops[0].PrimaryKey);
        Assert.Equal(EAutoStopStatus.Superseded, stops[0].Status);
        Assert.Equal(PendingStopPlanner.EarlierStopReason, stops[0].CancelReason);
        Assert.Equal(EAutoStopReason.Schedule, stops[1].Reason);
        Assert.Equal(rule.PrimaryKey, stops[1].ScheduleFk);
        Assert.Equal(EAutoStopStatus.Pending, stops[1].Status);
        Assert.Single(result.RaisedAlerts);

        var stopped = await evaluator.EvaluateAsync(At(23, 0), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, stopped.JobsQueued);
        Assert.Equal(ELifecycleJobTrigger.Schedule, (await store.DbContext.LifecycleJobs.SingleAsync()).Trigger);
    }

    /// <summary>A warning that stops earlier than the scheduled stop keeps its place and the occurrence is busy.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task EarlierMaximumRuntimeWarning_WinsOverTheScheduledStop()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store, configure: q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
        });
        await AddStopRuleAsync(store, settings, 23, 30);
        var limit = await ServerAutomationTestData.AddPendingStopAsync(
            store.DbContext,
            seed.Deployment!,
            EAutoStopReason.MaxRuntime,
            At(23, 25),
            1);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        await evaluator.EvaluateAsync(At(23, 21), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        var kept = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Equal(limit.PrimaryKey, kept.PrimaryKey);
        Assert.Equal(EAutoStopStatus.Pending, kept.Status);

        await evaluator.EvaluateAsync(At(23, 25), CancellationToken.None);
        await evaluator.EvaluateAsync(At(23, 30), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobTrigger.MaxRuntime, job.Trigger);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.SkippedBusy, run.Outcome);
    }

    /// <summary>A repeated pass, or a pass after a simulated crash, never stops the server twice.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StopRule_RepeatedPassesAndACrashBetweenScanAndReconcile_StopOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, settings) = await SeedAsync(store);
        await AddStopRuleAsync(store, settings, 23, 0);
        var state = await ServerAutomationTestData.SeedWatermarkAsync(store.DbContext, seed.Server, At(22, 59));
        var runner = ServerAutomationTestData.CreateScheduleRunner(store.DbContext);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        // The scan commits the pending stop, then the process "crashes" before the reconcile runs.
        await runner.ProcessDueAsync(seed.Server.PrimaryKey, At(23, 0), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        var scanned = Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
        Assert.Null(scanned.WarnedAt);
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());

        await evaluator.EvaluateAsync(At(23, 0), CancellationToken.None);
        await evaluator.EvaluateAsync(At(23, 0), CancellationToken.None);

        // The watermark commit is lost as well: the next pass sees the same occurrence again.
        store.DbContext.ChangeTracker.Clear();
        var persistedState = await store.DbContext.ServerAutomationStates.SingleAsync(q => q.PrimaryKey == state.PrimaryKey);
        persistedState.SchedulesEvaluatedThrough = At(22, 59);
        await store.DbContext.SaveChangesAsync();
        await evaluator.EvaluateAsync(At(23, 1), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Single(await store.DbContext.AutomationPendingStops.ToArrayAsync());
    }

    private sealed record StartSetup(HostingTestSeed Seed, ServerAutomationSettings Settings, ServerSchedule Rule);

    /// <summary>
    /// Seeds a server with a start rule at 18:00 that is by default consented for the current cost owner, funded and
    /// has no deployment.
    /// </summary>
    private static async Task<StartSetup> SeedStartAsync(
        HostingTestStore store,
        bool consent = true,
        bool funded = true,
        bool withDeployment = false)
    {
        var db = store.DbContext;
        var (seed, settings) = await SeedAsync(store, withDeployment);
        if (consent)
        {
            settings.StartConsentAt = At(10, 0);
            settings.StartConsentCostOwnerFk = seed.CostOwner.PrimaryKey;
        }

        if (funded)
            seed.CostOwner.Balance = 100m;
        await db.SaveChangesAsync();
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            db,
            settings,
            EServerScheduleAction.Start,
            EveryDay,
            Minute(18, 0),
            At(17, 0));
        return new StartSetup(seed, settings, rule);
    }

    private static async Task<ScheduleScanResult> ScanAsync(HostingTestStore store, StartSetup setup, Instant now)
        => await ServerAutomationTestData.CreateScheduleRunner(store.DbContext)
            .ProcessDueAsync(setup.Seed.Server.PrimaryKey, now, CancellationToken.None);

    /// <summary>
    /// Asserts that the only recorded effect of the occurrence is one run row and one skip audit entry.
    /// </summary>
    private static async Task AssertSkippedAsync(
        HostingTestStore store,
        StartSetup setup,
        EServerScheduleRunOutcome outcome,
        string reason,
        int jobs = 0)
    {
        store.DbContext.ChangeTracker.Clear();
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(outcome, run.Outcome);
        Assert.Equal(setup.Rule.PrimaryKey, run.ScheduleFk);
        Assert.Equal(At(18, 0), run.OccurrenceAt);
        Assert.Null(run.LifecycleJobFk);
        Assert.Contains(reason, run.Details);
        Assert.Equal(jobs, await store.DbContext.LifecycleJobs.CountAsync());
        var audit = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped)
                .ToArrayAsync());
        Assert.Contains(reason, audit.Summary);
        Assert.Contains(setup.Rule.PrimaryKey.ToString(), audit.Details);
        Assert.Contains($"Outcome={outcome}", audit.Details);
        Assert.Contains("2026-11-20", audit.Details);
        Assert.Equal(ECostImpact.NoCostImpact, audit.CostImpact);
        Assert.DoesNotContain(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.ScheduledStartRequested);
    }

    /// <summary>A consented, funded start rule queues one ordinary start job at its time.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithConsentAndFunds_QueuesOneStartJobAndRecordsTheRun()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var result = await evaluator.EvaluateAsync(At(18, 0), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, result.JobsQueued);
        var job = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobKind.Start, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.Schedule, job.Trigger);
        Assert.Null(job.RequestedByFk);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(setup.Rule.PrimaryKey, run.ScheduleFk);
        Assert.Equal(At(18, 0), run.OccurrenceAt);
        Assert.Equal(EServerScheduleRunOutcome.Started, run.Outcome);
        Assert.Equal(job.PrimaryKey, run.LifecycleJobFk);
        var requested = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledStartRequested)
                .ToArrayAsync());
        Assert.Null(requested.ActorFk);
        Assert.Equal(job.PrimaryKey, requested.LifecycleJobFk);
        Assert.DoesNotContain(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped);
        Assert.Single(await store.DbContext.GameServerDeployments.ToArrayAsync());
        Assert.Equal(At(18, 0), (await store.DbContext.ServerAutomationStates.SingleAsync()).SchedulesEvaluatedThrough);
    }

    /// <summary>Without recorded consent no start is queued and the skip is recorded.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithoutConsent_IsRecordedSkippedConsent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, consent: false);

        var result = await ScanAsync(store, setup, At(18, 0));

        Assert.Equal(0, result.StartsQueued);
        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedConsent, "consent");
        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
    }

    /// <summary>Consent recorded for another cost owner does not allow the current owner to be charged.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithConsentForAnotherCostOwner_IsRecordedSkippedConsent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);
        var other = await HostingTestData.SeedServerAsync(store.DbContext);
        setup.Settings.StartConsentCostOwnerFk = other.CostOwner.PrimaryKey;
        await store.DbContext.SaveChangesAsync();

        await ScanAsync(store, setup, At(18, 0));

        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedConsent, "consent");
    }

    /// <summary>A server that already has an unresolved deployment is not started again.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithAnUnresolvedDeployment_IsRecordedSkippedRunning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);

        await ScanAsync(store, setup, At(18, 0));

        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedRunning, "unresolved deployment");
        Assert.Single(await store.DbContext.GameServerDeployments.ToArrayAsync());
    }

    /// <summary>
    /// A start that meets a stop in flight is stored as waiting without any job or audit entry, stays one untouched row
    /// over later passes, and becomes exactly one ordinary start job once the stop finished, past the grace window.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WhileAStopJobIsInFlight_WaitsAndStartsOnceAfterTheStopFinished()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);
        var stop = await ServerAutomationTestData.AddJobAsync(
            store.DbContext,
            setup.Seed.Deployment!,
            ELifecycleJobStatus.Queued);
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var first = await evaluator.EvaluateAsync(At(18, 0), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(0, first.JobsQueued);
        var waiting = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, waiting.Outcome);
        Assert.Equal(setup.Rule.PrimaryKey, waiting.ScheduleFk);
        Assert.Equal(At(18, 0), waiting.OccurrenceAt);
        Assert.Null(waiting.LifecycleJobFk);
        Assert.Contains("waiting", waiting.Details);
        Assert.Equal(At(18, 0), waiting.RecordedAt);
        Assert.Equal(stop.PrimaryKey, Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync()).PrimaryKey);
        Assert.Empty(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped)
            .ToArrayAsync());

        await evaluator.EvaluateAsync(At(18, 0, 15), CancellationToken.None);
        await evaluator.EvaluateAsync(At(18, 10), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        var stillWaiting = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(waiting.PrimaryKey, stillWaiting.PrimaryKey);
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, stillWaiting.Outcome);
        Assert.Equal(At(18, 0), stillWaiting.RecordedAt);

        var deployment = await store.DbContext.GameServerDeployments.SingleAsync();
        deployment.Status = EDeploymentStatus.Stopped;
        deployment.TimeStampResolved = At(18, 30);
        (await store.DbContext.LifecycleJobs.SingleAsync()).Status = ELifecycleJobStatus.Succeeded;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var started = await evaluator.EvaluateAsync(At(18, 31), CancellationToken.None);
        var repeated = await evaluator.EvaluateAsync(At(18, 32), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, started.JobsQueued);
        Assert.Equal(0, repeated.JobsQueued);
        var start = Assert.Single(
            await store.DbContext.LifecycleJobs.Where(q => q.Kind == ELifecycleJobKind.Start).ToArrayAsync());
        Assert.Equal(ELifecycleJobTrigger.Schedule, start.Trigger);
        Assert.Null(start.RequestedByFk);
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(waiting.PrimaryKey, run.PrimaryKey);
        Assert.Equal(EServerScheduleRunOutcome.Started, run.Outcome);
        Assert.Equal(start.PrimaryKey, run.LifecycleJobFk);
        var requested = Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledStartRequested)
                .ToArrayAsync());
        Assert.Contains("WaitedForStop=true", requested.Details);
        Assert.DoesNotContain(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped);
    }

    /// <summary>A stop or terminate that is queued, running, retry-pending or waiting for its worker makes a start wait.</summary>
    /// <param name="kind">The kind of the job in flight.</param>
    /// <param name="status">The status of the job in flight.</param>
    /// <returns>A task completing after the assertions ran.</returns>
    [Theory]
    [InlineData(ELifecycleJobKind.Stop, ELifecycleJobStatus.Queued)]
    [InlineData(ELifecycleJobKind.Stop, ELifecycleJobStatus.Running)]
    [InlineData(ELifecycleJobKind.Stop, ELifecycleJobStatus.RetryPending)]
    [InlineData(ELifecycleJobKind.Stop, ELifecycleJobStatus.WaitingForWorker)]
    [InlineData(ELifecycleJobKind.Terminate, ELifecycleJobStatus.Queued)]
    [InlineData(ELifecycleJobKind.Terminate, ELifecycleJobStatus.Running)]
    [InlineData(ELifecycleJobKind.Terminate, ELifecycleJobStatus.RetryPending)]
    [InlineData(ELifecycleJobKind.Terminate, ELifecycleJobStatus.WaitingForWorker)]
    public async Task StartRule_WhileAStopOrTerminateIsActive_Waits(ELifecycleJobKind kind, ELifecycleJobStatus status)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);
        await ServerAutomationTestData.AddJobAsync(store.DbContext, setup.Seed.Deployment!, status, kind);

        var result = await ScanAsync(store, setup, At(18, 0));

        Assert.Equal(0, result.StartsQueued);
        store.DbContext.ChangeTracker.Clear();
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, run.Outcome);
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    /// <summary>
    /// A stop job that is still completing after its deployment resolved keeps the start waiting, and the next pass
    /// after the job succeeded starts once.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WhileTheResolvedStopJobIsStillCompleting_WaitsThenStartsOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);
        setup.Seed.Deployment!.Status = EDeploymentStatus.Stopped;
        setup.Seed.Deployment.TimeStampResolved = At(17, 55);
        await store.DbContext.SaveChangesAsync();
        var stop = await ServerAutomationTestData.AddJobAsync(
            store.DbContext,
            setup.Seed.Deployment,
            ELifecycleJobStatus.Running);

        var waiting = await ScanAsync(store, setup, At(18, 0));

        Assert.Equal(0, waiting.StartsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            EServerScheduleRunOutcome.WaitingForStop,
            (await store.DbContext.ServerScheduleRuns.SingleAsync()).Outcome);
        (await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == stop.PrimaryKey)).Status =
            ELifecycleJobStatus.Succeeded;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var started = await ScanAsync(store, setup, At(18, 1));
        var repeated = await ScanAsync(store, setup, At(18, 2));

        Assert.Equal(1, started.StartsQueued);
        Assert.Equal(0, repeated.StartsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(EServerScheduleRunOutcome.Started, (await store.DbContext.ServerScheduleRuns.SingleAsync()).Outcome);
        Assert.Single(await store.DbContext.LifecycleJobs.Where(q => q.Kind == ELifecycleJobKind.Start).ToArrayAsync());
    }

    /// <summary>A pending lifecycle action that is not a stop blocks the start.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithAPendingNonStopAction_IsRecordedSkippedBusy()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);
        setup.Seed.Deployment!.TimeStampResolved = At(10, 0);
        await store.DbContext.SaveChangesAsync();
        await ServerAutomationTestData.AddJobAsync(
            store.DbContext,
            setup.Seed.Deployment,
            ELifecycleJobStatus.Queued,
            ELifecycleJobKind.Update);

        await ScanAsync(store, setup, At(18, 0));

        store.DbContext.ChangeTracker.Clear();
        var run = Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.SkippedBusy, run.Outcome);
        Assert.Contains("pending", run.Details);
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped)
                .ToArrayAsync());
    }

    /// <summary>A frozen cost owner is skipped for cost.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithAFrozenCostOwner_IsRecordedSkippedCost()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);
        setup.Seed.CostOwner.IsFrozen = true;
        await store.DbContext.SaveChangesAsync();

        await ScanAsync(store, setup, At(18, 0));

        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedCost, "cannot pay");
    }

    /// <summary>A cost owner who cannot afford the initial slice is skipped for cost.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithAnUnaffordableCostOwner_IsRecordedSkippedCost()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, funded: false);

        await ScanAsync(store, setup, At(18, 0));

        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedCost, "cannot pay");
        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
    }

    /// <summary>A refusal of the lifecycle service is recorded as busy and leaves nothing else behind.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WhenTheLifecycleServiceRefuses_IsRecordedSkippedBusyWithNothingElsePersisted()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);
        setup.Seed.Server.IsLaunchDisabled = true;
        setup.Seed.Server.LaunchDisabledReason = "maintenance window";
        await store.DbContext.SaveChangesAsync();

        var result = await ScanAsync(store, setup, At(18, 0));

        Assert.Equal(0, result.StartsQueued);
        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedBusy, "maintenance window");
        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
        Assert.Single(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    /// <summary>A start older than the grace window is missed, one inside it still runs.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_OlderThanTheGraceWindow_IsRecordedSkippedMissed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);

        var result = await ScanAsync(store, setup, At(18, 15, 1));

        Assert.Equal(0, result.StartsQueued);
        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedMissed, "grace window");
    }

    /// <summary>A start exactly at the end of the grace window still runs.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_AtTheEndOfTheGraceWindow_StillRuns()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);

        var result = await ScanAsync(store, setup, At(18, 15));

        Assert.Equal(1, result.StartsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(EServerScheduleRunOutcome.Started, (await store.DbContext.ServerScheduleRuns.SingleAsync()).Outcome);
    }

    /// <summary>
    /// The outcomes are evaluated in the order missed, consent, stop in flight, running, busy, cost: a missed first
    /// evaluation and missing consent beat a stop in flight, and a stop in flight beats an unaffordable owner.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_OutcomeOrder_PrefersMissedThenConsentThenStopInFlightThenRunningThenCost()
    {
        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, withDeployment: true);
            await ServerAutomationTestData.AddJobAsync(store.DbContext, setup.Seed.Deployment!, ELifecycleJobStatus.Queued);
            await ScanAsync(store, setup, At(18, 30));
            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedMissed, "grace window", jobs: 1);
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, consent: false, withDeployment: true);
            await ServerAutomationTestData.AddJobAsync(store.DbContext, setup.Seed.Deployment!, ELifecycleJobStatus.Queued);
            await ScanAsync(store, setup, At(18, 0));
            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedConsent, "consent", jobs: 1);
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, funded: false, withDeployment: true);
            await ServerAutomationTestData.AddJobAsync(store.DbContext, setup.Seed.Deployment!, ELifecycleJobStatus.Queued);
            await ScanAsync(store, setup, At(18, 0));
            store.DbContext.ChangeTracker.Clear();
            Assert.Equal(
                EServerScheduleRunOutcome.WaitingForStop,
                (await store.DbContext.ServerScheduleRuns.SingleAsync()).Outcome);
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, consent: false, funded: false, withDeployment: true);
            await ScanAsync(store, setup, At(18, 30));
            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedMissed, "grace window");
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, consent: false, funded: false, withDeployment: true);
            await ScanAsync(store, setup, At(18, 0));
            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedConsent, "consent");
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, funded: false, withDeployment: true);
            await ScanAsync(store, setup, At(18, 0));
            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedRunning, "unresolved deployment");
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedStartAsync(store, funded: false);
            await ScanAsync(store, setup, At(18, 0));
            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedCost, "cannot pay");
        }
    }

    /// <summary>A repeated pass, and a pass after a lost watermark commit, queue exactly one start.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_RepeatedPassesAndALostWatermarkCommit_StartOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store);
        var first = await ScanAsync(store, setup, At(18, 0));
        var second = await ScanAsync(store, setup, At(18, 0));

        store.DbContext.ChangeTracker.Clear();
        var state = await store.DbContext.ServerAutomationStates.SingleAsync();
        state.SchedulesEvaluatedThrough = At(17, 59);
        await store.DbContext.SaveChangesAsync();
        var third = await ScanAsync(store, setup, At(18, 1));

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, first.StartsQueued);
        Assert.Equal(0, second.StartsQueued);
        Assert.Equal(0, third.StartsQueued);
        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Single(await store.DbContext.ServerScheduleRuns.ToArrayAsync());
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledStartRequested)
                .ToArrayAsync());
    }

    /// <summary>
    /// Seeds a start rule whose 18:00 occurrence has been stored as waiting for a queued stop of the running
    /// deployment.
    /// </summary>
    private static async Task<StartSetup> SeedWaitingStartAsync(HostingTestStore store)
    {
        var setup = await SeedStartAsync(store, withDeployment: true);
        await ServerAutomationTestData.AddJobAsync(store.DbContext, setup.Seed.Deployment!, ELifecycleJobStatus.Queued);
        await ScanAsync(store, setup, At(18, 0));
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            EServerScheduleRunOutcome.WaitingForStop,
            (await store.DbContext.ServerScheduleRuns.SingleAsync()).Outcome);
        return setup;
    }

    /// <summary>
    /// Finishes the only stop job of the scenario: the deployment is resolved and the job succeeded.
    /// </summary>
    private static async Task FinishStopAsync(HostingTestStore store, Instant resolvedAt)
    {
        var db = store.DbContext;
        db.ChangeTracker.Clear();
        var deployment = await db.GameServerDeployments.SingleAsync();
        deployment.Status = EDeploymentStatus.Stopped;
        deployment.TimeStampResolved = resolvedAt;
        (await db.LifecycleJobs.SingleAsync()).Status = ELifecycleJobStatus.Succeeded;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// A waiting start whose stop ended failed and waits for a person is closed on the same row with the real blocker;
    /// nothing is started and nothing is escalated.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WhenTheStopFailed_ClosesTheWaitingStartAsSkippedStopUnfinished()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedWaitingStartAsync(store);
        var waitingKey = (await store.DbContext.ServerScheduleRuns.SingleAsync()).PrimaryKey;
        (await store.DbContext.LifecycleJobs.SingleAsync()).Status = ELifecycleJobStatus.Failed;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var result = await ScanAsync(store, setup, At(18, 5));

        Assert.Equal(0, result.StartsQueued);
        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedStopUnfinished, "failed", jobs: 1);
        Assert.Equal(waitingKey, (await store.DbContext.ServerScheduleRuns.SingleAsync()).PrimaryKey);
        var job = await store.DbContext.LifecycleJobs.SingleAsync();
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobStatus.Failed, job.Status);
    }

    /// <summary>A start that comes due while a failed stop keeps the deployment unresolved is not reported as running.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WithAFailedStopHoldingTheDeployment_IsRecordedSkippedStopUnfinished()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);
        await ServerAutomationTestData.AddJobAsync(store.DbContext, setup.Seed.Deployment!, ELifecycleJobStatus.Failed);

        await ScanAsync(store, setup, At(18, 0));

        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedStopUnfinished, "failed", jobs: 1);
    }

    /// <summary>
    /// A waiting start is closed when the rule's next occurrence comes due while the stop is still running, and the
    /// newer occurrence takes over and starts once after the stop finished.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WhenTheNextOccurrenceComesDue_ClosesTheOldWaitingStartAndTheNewOneStartsOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedWaitingStartAsync(store);
        var nextDay = Duration.FromDays(1);

        await ScanAsync(store, setup, At(18, 0) + nextDay);

        store.DbContext.ChangeTracker.Clear();
        var runs = await store.DbContext.ServerScheduleRuns.OrderBy(q => q.OccurrenceAt).ToArrayAsync();
        Assert.Equal(2, runs.Length);
        Assert.Equal(At(18, 0), runs[0].OccurrenceAt);
        Assert.Equal(EServerScheduleRunOutcome.SkippedStopUnfinished, runs[0].Outcome);
        Assert.Contains("next start", runs[0].Details);
        Assert.Equal(At(18, 0) + nextDay, runs[1].OccurrenceAt);
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, runs[1].Outcome);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledRunSkipped)
                .ToArrayAsync());

        await FinishStopAsync(store, At(18, 20) + nextDay);
        var started = await ScanAsync(store, setup, At(18, 31) + nextDay);
        var repeated = await ScanAsync(store, setup, At(18, 32) + nextDay);

        Assert.Equal(1, started.StartsQueued);
        Assert.Equal(0, repeated.StartsQueued);
        store.DbContext.ChangeTracker.Clear();
        var start = Assert.Single(
            await store.DbContext.LifecycleJobs.Where(q => q.Kind == ELifecycleJobKind.Start).ToArrayAsync());
        runs = await store.DbContext.ServerScheduleRuns.OrderBy(q => q.OccurrenceAt).ToArrayAsync();
        Assert.Equal(EServerScheduleRunOutcome.SkippedStopUnfinished, runs[0].Outcome);
        Assert.Equal(EServerScheduleRunOutcome.Started, runs[1].Outcome);
        Assert.Equal(start.PrimaryKey, runs[1].LifecycleJobFk);
    }

    /// <summary>A waiting start of a rule that was changed after the occurrence is closed as changed.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WhenTheRuleWasChangedWhileWaiting_IsClosedAsChanged()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedWaitingStartAsync(store);
        var rule = await store.DbContext.ServerSchedules.SingleAsync();
        rule.MinuteOfDay = Minute(18, 30);
        rule.EffectiveFrom = At(18, 10);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var result = await ScanAsync(store, setup, At(18, 20));

        Assert.Equal(0, result.StartsQueued);
        await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedStopUnfinished, "changed", jobs: 1);
    }

    /// <summary>
    /// When the stop has finished the normal checks decide on the same row: consent for the current cost owner, the
    /// owner's funds, a refusal of the lifecycle service and a server that is running again.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_AfterTheStopFinished_ClosesTheSameRowWithTheNormalChecks()
    {
        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedWaitingStartAsync(store);
            var other = await HostingTestData.SeedServerAsync(store.DbContext);
            await FinishStopAsync(store, At(18, 20));
            (await store.DbContext.ServerAutomationSettings.SingleAsync()).StartConsentCostOwnerFk =
                other.CostOwner.PrimaryKey;
            await store.DbContext.SaveChangesAsync();
            store.DbContext.ChangeTracker.Clear();

            await ScanAsync(store, setup, At(18, 30));

            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedConsent, "consent", jobs: 1);
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedWaitingStartAsync(store);
            await FinishStopAsync(store, At(18, 20));
            (await store.DbContext.CostOwners.SingleAsync()).Balance = 0m;
            await store.DbContext.SaveChangesAsync();
            store.DbContext.ChangeTracker.Clear();

            await ScanAsync(store, setup, At(18, 30));

            await AssertSkippedAsync(store, setup, EServerScheduleRunOutcome.SkippedCost, "cannot pay", jobs: 1);
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            var setup = await SeedWaitingStartAsync(store);
            await FinishStopAsync(store, At(18, 20));
            var server = await store.DbContext.GameServers.SingleAsync();
            server.IsLaunchDisabled = true;
            server.LaunchDisabledReason = "maintenance window";
            await store.DbContext.SaveChangesAsync();
            store.DbContext.ChangeTracker.Clear();

            var result = await ScanAsync(store, setup, At(18, 30));

            Assert.Equal(0, result.StartsQueued);
            await AssertSkippedAsync(
                store,
                setup,
                EServerScheduleRunOutcome.SkippedBusy,
                "maintenance window",
                jobs: 1);
        }

        await using (var store = await HostingTestStore.CreateAsync())
        {
            // The stop job finished but someone started the server again: its deployment is unresolved once more.
            var setup = await SeedWaitingStartAsync(store);
            (await store.DbContext.LifecycleJobs.SingleAsync()).Status = ELifecycleJobStatus.Succeeded;
            await store.DbContext.SaveChangesAsync();
            store.DbContext.ChangeTracker.Clear();

            await ScanAsync(store, setup, At(18, 30));

            await AssertSkippedAsync(
                store,
                setup,
                EServerScheduleRunOutcome.SkippedRunning,
                "unresolved deployment",
                jobs: 1);
        }
    }

    /// <summary>
    /// A stop that came due in the same catch-up pass keeps a later start waiting, and the start runs once after the
    /// stop finished.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_AfterAStopThatCameDueInTheSameCatchUpPass_WaitsAndStartsOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedStartAsync(store, withDeployment: true);
        setup.Seed.Deployment!.TimeStampStarted = At(10, 0);
        setup.Rule.MinuteOfDay = Minute(18, 1);
        await store.DbContext.SaveChangesAsync();
        await AddStopRuleAsync(store, setup.Settings, 18, 0, effectiveFrom: At(17, 0));
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);

        var catchUp = await evaluator.EvaluateAsync(At(18, 5), CancellationToken.None);

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, catchUp.JobsQueued);
        var stop = Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
        Assert.Equal(ELifecycleJobKind.Stop, stop.Kind);
        Assert.Equal(ELifecycleJobTrigger.Schedule, stop.Trigger);
        var runs = await store.DbContext.ServerScheduleRuns.OrderBy(q => q.OccurrenceAt).ToArrayAsync();
        Assert.Equal(2, runs.Length);
        Assert.Equal(EServerScheduleRunOutcome.Stopped, runs[0].Outcome);
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, runs[1].Outcome);
        Assert.Equal(setup.Rule.PrimaryKey, runs[1].ScheduleFk);

        await FinishStopAsync(store, At(18, 20));
        var started = await evaluator.EvaluateAsync(At(18, 40), CancellationToken.None);
        var repeated = await evaluator.EvaluateAsync(At(18, 41), CancellationToken.None);

        Assert.Equal(1, started.JobsQueued);
        Assert.Equal(0, repeated.JobsQueued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Single(await store.DbContext.LifecycleJobs.Where(q => q.Kind == ELifecycleJobKind.Start).ToArrayAsync());
        Assert.Equal(
            EServerScheduleRunOutcome.Started,
            (await store.DbContext.ServerScheduleRuns.SingleAsync(q => q.ScheduleFk == setup.Rule.PrimaryKey)).Outcome);
    }

    /// <summary>
    /// Repeated passes and a rewound watermark, while the start waits and after it was queued, keep one row, one start
    /// job and one start audit entry.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task StartRule_WaitingAndRewoundWatermark_RepeatedPassesStartOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var setup = await SeedWaitingStartAsync(store);

        foreach (var at in new[] { At(18, 1), At(18, 2) })
        {
            await RewindWatermarkAsync(store, At(17, 59));
            Assert.Equal(0, (await ScanAsync(store, setup, at)).StartsQueued);
        }

        store.DbContext.ChangeTracker.Clear();
        var waiting = await store.DbContext.ServerScheduleRuns.SingleAsync();
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, waiting.Outcome);
        Assert.Equal(At(18, 0), waiting.RecordedAt);
        await FinishStopAsync(store, At(18, 20));

        var queued = 0;
        foreach (var at in new[] { At(18, 31), At(18, 32), At(18, 33) })
        {
            await RewindWatermarkAsync(store, At(17, 59));
            queued += (await ScanAsync(store, setup, at)).StartsQueued;
        }

        Assert.Equal(1, queued);
        store.DbContext.ChangeTracker.Clear();
        Assert.Single(await store.DbContext.LifecycleJobs.Where(q => q.Kind == ELifecycleJobKind.Start).ToArrayAsync());
        Assert.Equal(EServerScheduleRunOutcome.Started, (await store.DbContext.ServerScheduleRuns.SingleAsync()).Outcome);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.ScheduledStartRequested)
                .ToArrayAsync());
    }

    /// <summary>
    /// Moves the schedule watermark back, like a lost watermark commit.
    /// </summary>
    private static async Task RewindWatermarkAsync(HostingTestStore store, Instant through)
    {
        store.DbContext.ChangeTracker.Clear();
        (await store.DbContext.ServerAutomationStates.SingleAsync()).SchedulesEvaluatedThrough = through;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
    }
}
