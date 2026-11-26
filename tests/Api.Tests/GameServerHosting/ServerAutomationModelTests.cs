using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// AUT-06 proof that every automation timer fact is persisted and that the database itself refuses a second open
/// warning, a second settings row and a second recording of one schedule occurrence.
/// </summary>
public sealed class ServerAutomationModelTests
{
    /// <summary>
    /// The instant used as the creation time of every row.
    /// </summary>
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// A settings row with one schedule rule reads back in a fresh context with the same values and the child.
    /// </summary>
    /// <returns>A task completing after the persisted rows were verified.</returns>
    [Fact]
    public async Task Settings_WithOneRule_RoundTripsThroughFreshContext()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var settings = NewSettings(seed.Server.PrimaryKey);
        settings.Schedules.Add(NewSchedule(settings, seed.Server.PrimaryKey));
        store.DbContext.ServerAutomationSettings.Add(settings);
        await store.DbContext.SaveChangesAsync();

        await using var fresh = CreateFreshContext(store);
        var loaded = await fresh.ServerAutomationSettings
            .Include(q => q.Schedules)
            .SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey);
        Assert.True(loaded.IdleShutdownEnabled);
        Assert.Equal(45, loaded.IdleShutdownMinutes);
        Assert.True(loaded.MaxRuntimeEnabled);
        Assert.Equal(6, loaded.MaxRuntimeHours);
        Assert.Equal("Europe/Berlin", loaded.TimeZone);
        Assert.Equal(3, loaded.Version);
        var rule = Assert.Single(loaded.Schedules);
        Assert.Equal(EServerScheduleAction.Stop, rule.Action);
        Assert.Equal(127, rule.DaysMask);
        Assert.Equal(1380, rule.MinuteOfDay);
        Assert.Equal(seed.Server.PrimaryKey, rule.GameServerFk);
    }

    /// <summary>
    /// An evaluator state row with an idle timer and an evaluation watermark reads back unchanged.
    /// </summary>
    /// <returns>A task completing after the persisted row was verified.</returns>
    [Fact]
    public async Task State_WithIdleTimerAndWatermark_RoundTrips()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var idleSince = Now + Duration.FromMinutes(5);
        var through = Now + Duration.FromMinutes(10);
        store.DbContext.ServerAutomationStates.Add(new ServerAutomationState
        {
            PrimaryKey                = Guid.NewGuid(),
            GameServerFk              = seed.Server.PrimaryKey,
            IdleDeploymentFk          = seed.Deployment!.PrimaryKey,
            IdleSince                 = idleSince,
            SchedulesEvaluatedThrough = through,
            TimeStampUpdated          = Now,
        });
        await store.DbContext.SaveChangesAsync();

        await using var fresh = CreateFreshContext(store);
        var loaded = await fresh.ServerAutomationStates.SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey);
        Assert.Equal(seed.Deployment.PrimaryKey, loaded.IdleDeploymentFk);
        Assert.Equal(idleSince, loaded.IdleSince);
        Assert.Null(loaded.IdleHeldSince);
        Assert.Equal(through, loaded.SchedulesEvaluatedThrough);
    }

    /// <summary>
    /// A second pending stop for one deployment is rejected, while a resolved row for the same deployment saves.
    /// </summary>
    /// <returns>A task completing after the constraint behavior was verified.</returns>
    [Fact]
    public async Task PendingStop_SecondPendingForDeployment_IsRejectedButCancelledSaves()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        store.DbContext.AutomationPendingStops.Add(
            NewPendingStop(seed.Server.PrimaryKey, seed.Deployment!.PrimaryKey, EAutoStopStatus.Pending));
        await store.DbContext.SaveChangesAsync();

        await using var second = CreateFreshContext(store);
        second.AutomationPendingStops.Add(
            NewPendingStop(seed.Server.PrimaryKey, seed.Deployment.PrimaryKey, EAutoStopStatus.Pending));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        await using var third = CreateFreshContext(store);
        third.AutomationPendingStops.Add(
            NewPendingStop(seed.Server.PrimaryKey, seed.Deployment.PrimaryKey, EAutoStopStatus.Cancelled));
        await third.SaveChangesAsync();
        Assert.Equal(2, await third.AutomationPendingStops.CountAsync());
    }

    /// <summary>
    /// A second settings row for one server is rejected.
    /// </summary>
    /// <returns>A task completing after the constraint behavior was verified.</returns>
    [Fact]
    public async Task Settings_SecondRowForServer_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        store.DbContext.ServerAutomationSettings.Add(NewSettings(seed.Server.PrimaryKey));
        await store.DbContext.SaveChangesAsync();

        await using var second = CreateFreshContext(store);
        second.ServerAutomationSettings.Add(NewSettings(seed.Server.PrimaryKey));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    /// <summary>
    /// A second run of one schedule occurrence is rejected while another occurrence of the same rule saves.
    /// </summary>
    /// <returns>A task completing after the constraint behavior was verified.</returns>
    [Fact]
    public async Task ScheduleRun_SameOccurrenceTwice_IsRejectedButOtherOccurrenceSaves()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, schedule) = await SeedScheduleAsync(store);
        var occurrence = Now + Duration.FromHours(1);
        store.DbContext.ServerScheduleRuns.Add(NewRun(schedule, occurrence));
        await store.DbContext.SaveChangesAsync();

        await using var second = CreateFreshContext(store);
        second.ServerScheduleRuns.Add(NewRun(schedule, occurrence));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        await using var third = CreateFreshContext(store);
        third.ServerScheduleRuns.Add(NewRun(schedule, occurrence + Duration.FromDays(1)));
        await third.SaveChangesAsync();
        Assert.Equal(2, await third.ServerScheduleRuns.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey));
    }

    /// <summary>
    /// Deleting a schedule rule deletes its runs and clears the rule reference of a pending stop.
    /// </summary>
    /// <returns>A task completing after the delete behavior was verified.</returns>
    [Fact]
    public async Task Schedule_Deleted_CascadesRunsAndClearsPendingStopReference()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, schedule) = await SeedScheduleAsync(store);
        store.DbContext.ServerScheduleRuns.Add(NewRun(schedule, Now + Duration.FromHours(1)));
        var pending = NewPendingStop(seed.Server.PrimaryKey, seed.Deployment!.PrimaryKey, EAutoStopStatus.Pending);
        pending.Reason = EAutoStopReason.Schedule;
        pending.ScheduleFk = schedule.PrimaryKey;
        pending.ScheduleOccurrenceAt = Now + Duration.FromHours(1);
        store.DbContext.AutomationPendingStops.Add(pending);
        await store.DbContext.SaveChangesAsync();

        await using var fresh = CreateFreshContext(store);
        var toDelete = await fresh.ServerSchedules.SingleAsync(q => q.PrimaryKey == schedule.PrimaryKey);
        fresh.ServerSchedules.Remove(toDelete);
        await fresh.SaveChangesAsync();

        await using var verify = CreateFreshContext(store);
        Assert.Equal(0, await verify.ServerScheduleRuns.CountAsync());
        var stop = await verify.AutomationPendingStops.SingleAsync(q => q.PrimaryKey == pending.PrimaryKey);
        Assert.Null(stop.ScheduleFk);
        Assert.NotNull(stop.ScheduleOccurrenceAt);
    }

    /// <summary>
    /// A lifecycle job saved without a trigger reads back as manual, and an automatic trigger round-trips with its
    /// limit.
    /// </summary>
    /// <returns>A task completing after the persisted rows were verified.</returns>
    [Fact]
    public async Task LifecycleJob_TriggerDefaultsToManualAndAutomaticTriggerRoundTrips()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var manual = NewJob(seed.Server.PrimaryKey, seed.Deployment!.PrimaryKey, 1);
        manual.Status = ELifecycleJobStatus.Succeeded;
        var idle = NewJob(seed.Server.PrimaryKey, seed.Deployment.PrimaryKey, 2);
        idle.Trigger = ELifecycleJobTrigger.Idle;
        idle.TriggerLimit = 30;
        store.DbContext.LifecycleJobs.AddRange(manual, idle);
        await store.DbContext.SaveChangesAsync();

        await using var fresh = CreateFreshContext(store);
        var loadedManual = await fresh.LifecycleJobs.SingleAsync(q => q.PrimaryKey == manual.PrimaryKey);
        Assert.Equal(ELifecycleJobTrigger.Manual, loadedManual.Trigger);
        Assert.Null(loadedManual.TriggerLimit);
        var loadedIdle = await fresh.LifecycleJobs.SingleAsync(q => q.PrimaryKey == idle.PrimaryKey);
        Assert.Equal(ELifecycleJobTrigger.Idle, loadedIdle.Trigger);
        Assert.Equal(30, loadedIdle.TriggerLimit);
    }

    /// <summary>
    /// Two threshold alerts for one deployment and threshold are rejected, and an alert saved without a kind reads
    /// back as a cost threshold alert.
    /// </summary>
    /// <returns>A task completing after the constraint behavior was verified.</returns>
    [Fact]
    public async Task CostAlert_DuplicateThresholdAlert_IsRejectedAndKindDefaultsToThreshold()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var first = NewAlert(seed, 5m, null);
        store.DbContext.CostAlerts.Add(first);
        await store.DbContext.SaveChangesAsync();

        await using var second = CreateFreshContext(store);
        second.CostAlerts.Add(NewAlert(seed, 5m, null));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        await using var verify = CreateFreshContext(store);
        var loaded = await verify.CostAlerts.SingleAsync(q => q.PrimaryKey == first.PrimaryKey);
        Assert.Equal(ECostAlertKind.CostThreshold, loaded.Kind);
        Assert.Null(loaded.PendingStopFk);
    }

    /// <summary>
    /// Automatic stop alerts of one deployment coexist for different pending stops, but a second alert for the same
    /// pending stop is rejected.
    /// </summary>
    /// <returns>A task completing after the constraint behavior was verified.</returns>
    [Fact]
    public async Task CostAlert_AutoStopAlerts_AreUniquePerPendingStop()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var cancelled = NewPendingStop(seed.Server.PrimaryKey, seed.Deployment!.PrimaryKey, EAutoStopStatus.Cancelled);
        var pending = NewPendingStop(seed.Server.PrimaryKey, seed.Deployment.PrimaryKey, EAutoStopStatus.Pending);
        store.DbContext.AutomationPendingStops.AddRange(cancelled, pending);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.CostAlerts.AddRange(
            NewAlert(seed, 0m, cancelled.PrimaryKey),
            NewAlert(seed, 0m, pending.PrimaryKey));
        await store.DbContext.SaveChangesAsync();

        await using var second = CreateFreshContext(store);
        second.CostAlerts.Add(NewAlert(seed, 0m, pending.PrimaryKey));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        await using var verify = CreateFreshContext(store);
        Assert.Equal(2, await verify.CostAlerts.CountAsync(q => q.Kind == ECostAlertKind.AutoStop));
    }

    private static async Task<(HostingTestSeed Seed, ServerSchedule Schedule)> SeedScheduleAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var settings = NewSettings(seed.Server.PrimaryKey);
        var schedule = NewSchedule(settings, seed.Server.PrimaryKey);
        settings.Schedules.Add(schedule);
        store.DbContext.ServerAutomationSettings.Add(settings);
        await store.DbContext.SaveChangesAsync();
        return (seed, schedule);
    }

    private static ServerScheduleRun NewRun(ServerSchedule schedule, Instant occurrence)
    {
        return new ServerScheduleRun
        {
            PrimaryKey   = Guid.NewGuid(),
            ScheduleFk   = schedule.PrimaryKey,
            GameServerFk = schedule.GameServerFk,
            OccurrenceAt = occurrence,
            Outcome      = EServerScheduleRunOutcome.Stopped,
            RecordedAt   = occurrence,
        };
    }

    private static LifecycleJob NewJob(Guid serverId, Guid deploymentId, long sequence)
    {
        return new LifecycleJob
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = serverId,
            DeploymentFk     = deploymentId,
            Kind             = ELifecycleJobKind.Stop,
            ActionIdentifier = "stop",
            Sequence         = sequence,
            Status           = ELifecycleJobStatus.Queued,
            Stage            = "queued",
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        };
    }

    private static CostAlert NewAlert(HostingTestSeed seed, decimal threshold, Guid? pendingStopId)
    {
        var alert = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = seed.Deployment!.PrimaryKey,
            GameServerFk     = seed.Server.PrimaryKey,
            CostOwnerFk      = seed.CostOwner.PrimaryKey,
            PendingStopFk    = pendingStopId,
            ThresholdAmount  = threshold,
            AccruedAmount    = 0m,
            TimeStampCreated = Now,
        };
        if (pendingStopId is not null)
            alert.Kind = ECostAlertKind.AutoStop;
        return alert;
    }

    private static NodaTimeSqliteApiDbContext CreateFreshContext(HostingTestStore store)
    {
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(store.Connection)
            .Options;
        return new NodaTimeSqliteApiDbContext(options);
    }

    private static ServerAutomationSettings NewSettings(Guid serverId)
    {
        return new ServerAutomationSettings
        {
            PrimaryKey           = Guid.NewGuid(),
            GameServerFk         = serverId,
            IdleShutdownEnabled  = true,
            IdleShutdownMinutes  = 45,
            MaxRuntimeEnabled    = true,
            MaxRuntimeHours      = 6,
            TimeZone             = "Europe/Berlin",
            Version              = 3,
            TimeStampCreated     = Now,
            TimeStampUpdated     = Now,
        };
    }

    private static ServerSchedule NewSchedule(ServerAutomationSettings settings, Guid serverId)
    {
        return new ServerSchedule
        {
            PrimaryKey       = Guid.NewGuid(),
            SettingsFk       = settings.PrimaryKey,
            GameServerFk     = serverId,
            Action           = EServerScheduleAction.Stop,
            DaysMask         = 127,
            MinuteOfDay      = 1380,
            Position         = 0,
            EffectiveFrom    = Now,
            TimeStampCreated = Now,
        };
    }

    private static AutomationPendingStop NewPendingStop(Guid serverId, Guid deploymentId, EAutoStopStatus status)
    {
        return new AutomationPendingStop
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = serverId,
            DeploymentFk     = deploymentId,
            Reason           = EAutoStopReason.Idle,
            Limit            = 30,
            WarnedAt         = Now,
            StopsAt          = Now + Duration.FromMinutes(5),
            Status           = status,
            ResolvedAt       = status == EAutoStopStatus.Pending ? null : Now,
            TimeStampCreated = Now,
        };
    }
}
