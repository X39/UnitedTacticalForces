using Microsoft.EntityFrameworkCore;
using Api.Tests.ServerAutomation;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the development UAT seeder creates a running session that has already cost money through a normal
/// charged provider-cost slice, a running session with a week of tiered metric history, the six automation scenarios
/// UAT 11 to 16, and that seeding repeatedly changes nothing.
/// </summary>
public sealed class UatFleetScenarioSeederTests
{
    /// <summary>
    /// The instant every seeding of these tests is anchored to.
    /// </summary>
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_800_000_000);

    /// <summary>
    /// The costing scenario creates a running, cost-possible deployment with the backdated authorization and rate.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CostingScenario_CreatesRunningPossibleCostDeployment()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var deployment = await LoadCostingDeploymentAsync(store);

        Assert.Equal(EDeploymentStatus.Running, deployment.Status);
        Assert.True(deployment.PossibleCost);
        Assert.Equal(Now + Duration.FromMinutes(15), deployment.CostAuthorizationExpiresAt);
        Assert.Equal(Now - Duration.FromMinutes(115), deployment.TimeStampStarted);
        Assert.Equal(0.60m, deployment.EstimatedHourlyCostEur);
    }

    /// <summary>
    /// The deployment carries exactly one provider-cost slice and the owner balance reflects the seed credit minus that
    /// charge.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CostingScenario_ChargesOneBackdatedSliceAgainstTheOwnerBalance()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var deployment = await LoadCostingDeploymentAsync(store);
        var entry = Assert.Single(
            await store.DbContext.HostingCostLedgerEntries
                .Where(q => q.DeploymentFk == deployment.PrimaryKey)
                .ToListAsync());
        var owner = await store.DbContext.CostOwners.SingleAsync(q => q.PrimaryKey == seed.CostOwner.PrimaryKey);

        Assert.Equal(EHostingLedgerEntryKind.ProviderCostSlice, entry.Kind);
        Assert.Equal(Now - Duration.FromMinutes(115), entry.SliceStartsAt);
        Assert.Equal(Now + Duration.FromMinutes(15), entry.SliceEndsAt);
        Assert.Equal(-1.3000m, entry.Amount);
        Assert.Equal(0.60m, entry.EstimatedHourlyRate);
        Assert.Equal(HetznerRuntimeProvider.Identifier, entry.ProviderIdentifier);
        Assert.Equal(998.7000m, owner.Balance);
        Assert.Equal(owner.Balance, entry.BalanceAfter);
    }

    /// <summary>
    /// The accrual calculator reports 1.20 EUR at the seeding instant and five minutes later, and steps to 1.80 EUR one
    /// second after the third started hour begins.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CostingScenario_ShowsOnePointTwoEuroThenStepsToOnePointEightEuro()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var deployment = await LoadCostingDeploymentAsync(store);
        var entry = await store.DbContext.HostingCostLedgerEntries
            .SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey);
        var slice = new AccruedCostSlice(
            entry.SliceStartsAt!.Value, entry.SliceEndsAt!.Value, -entry.Amount, entry.EstimatedHourlyRate);
        var unit = HetznerRuntimeProvider.ProviderBillingUnit;

        var atSeeding = CostAccrualCalculator.Calculate([slice], deployment, unit, Now);
        var beforeStep = CostAccrualCalculator.Calculate([slice], deployment, unit, Now + Duration.FromMinutes(5));
        var afterStep = CostAccrualCalculator.Calculate(
            [slice], deployment, unit, Now + Duration.FromMinutes(5) + Duration.FromSeconds(1));

        Assert.Equal(1.2000m, atSeeding.Amount);
        Assert.Equal(1.2000m, beforeStep.Amount);
        Assert.Equal(1.8000m, afterStep.Amount);
    }

    /// <summary>
    /// A blank owner email becomes the UAT address while an existing email is left untouched.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OwnerEmail_IsFilledOnlyWhenBlank()
    {
        await using var blankStore = await HostingTestStore.CreateAsync();
        var blankSeed = await SeedAsync(blankStore);
        Assert.Equal(
            UatFleetScenarioSeeder.OwnerEmailAddress,
            (await blankStore.DbContext.Users.SingleAsync(q => q.PrimaryKey == blankSeed.User.PrimaryKey)).EMail);

        await using var keptStore = await HostingTestStore.CreateAsync();
        var keptSeed = await HostingTestData.SeedServerAsync(keptStore.DbContext);
        keptSeed.User.EMail = "operator@example.org";
        await keptStore.DbContext.SaveChangesAsync();
        await UatFleetScenarioSeeder.SeedAsync(
            keptStore.DbContext,
            keptSeed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: Now);

        Assert.Equal(
            "operator@example.org",
            (await keptStore.DbContext.Users.SingleAsync(q => q.PrimaryKey == keptSeed.User.PrimaryKey)).EMail);
    }

    /// <summary>
    /// Seeding twice adds no second costing server and no second slice.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Seeding_IsIdempotent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        await UatFleetScenarioSeeder.SeedAsync(
            store.DbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: Now);

        Assert.Equal(
            1,
            await store.DbContext.GameServers.CountAsync(q => q.Title == UatFleetScenarioSeeder.CostingScenarioTitle));
        Assert.Equal(
            1,
            await store.DbContext.HostingCostLedgerEntries
                .CountAsync(q => q.Kind == EHostingLedgerEntryKind.ProviderCostSlice));
        Assert.Equal(998.7000m, (await store.DbContext.CostOwners.SingleAsync(q => q.UserFk != null)).Balance);
    }

    /// <summary>
    /// The seed credit is exactly one bank-transfer payment of 1000 EUR for the operator's cost owner, booked before the
    /// costing slice, and exactly one matching audit record exists.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SeedCredit_IsOneAuditedPaymentBookedBeforeTheCostingSlice()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var deployment = await LoadCostingDeploymentAsync(store);
        var credit = await store.DbContext.HostingCostLedgerEntries
            .SingleAsync(q => q.RecordIdentifier == UatFleetScenarioSeeder.CreditRecordIdentifier);
        var slice = await store.DbContext.HostingCostLedgerEntries
            .SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey);
        var audit = await store.DbContext.HostingAuditRecords
            .SingleAsync(q => q.Kind == EHostingAuditEventKind.BankTransferPaymentRecorded);

        Assert.Equal(EHostingLedgerEntryKind.BankTransferPayment, credit.Kind);
        Assert.Equal(UatFleetScenarioSeeder.CreditAmountEur, credit.Amount);
        Assert.Equal(1000.0000m, credit.Amount);
        Assert.Equal(1000.0000m, credit.BalanceAfter);
        Assert.Equal(seed.CostOwner.PrimaryKey, credit.CostOwnerFk);
        Assert.Equal(seed.User.PrimaryKey, credit.RecordedByFk);
        Assert.Null(credit.DeploymentFk);
        Assert.Equal(UatFleetScenarioSeeder.CreditExternalReference, credit.ExternalReference);
        Assert.Equal("UAT-SEED-CREDIT", credit.ExternalReference);
        Assert.False(string.IsNullOrWhiteSpace(credit.Reason));
        Assert.True(credit.TimeStampEffective < slice.TimeStampEffective);
        Assert.Equal(seed.CostOwner.PrimaryKey, audit.CostOwnerFk);
        Assert.Equal(seed.User.PrimaryKey, audit.ActorFk);
        Assert.Equal(ECostImpact.NoCostImpact, audit.CostImpact);
        Assert.Contains(credit.PrimaryKey.ToString(), audit.Details);
    }

    /// <summary>
    /// Seeding again books no second credit and no second audit record, and the owner balance stays at the seed credit
    /// minus the costing slice.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SeedCredit_IsNotBookedAgainOnReseed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        await UatFleetScenarioSeeder.SeedAsync(
            store.DbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: Now);

        Assert.Equal(
            1,
            await store.DbContext.HostingCostLedgerEntries
                .CountAsync(q => q.RecordIdentifier == UatFleetScenarioSeeder.CreditRecordIdentifier));
        Assert.Equal(
            1,
            await store.DbContext.HostingAuditRecords
                .CountAsync(q => q.Kind == EHostingAuditEventKind.BankTransferPaymentRecorded));
        Assert.Equal(998.7000m, (await store.DbContext.CostOwners.SingleAsync(q => q.UserFk != null)).Balance);
    }

    /// <summary>
    /// The metrics scenario creates a running possible-cost deployment with a resource and one online primary worker
    /// that carries process evidence.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_CreatesRunningDeploymentWithOnlinePrimaryWorker()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedAsync(store);

        var deployment = await store.DbContext.GameServerDeployments
            .Include(q => q.GameServer)
            .SingleAsync(q => q.GameServer!.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        var worker = await store.DbContext.LifecycleWorkers.SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey);

        Assert.Equal(EDeploymentStatus.Running, deployment.Status);
        Assert.True(deployment.PossibleCost);
        Assert.Equal(
            1,
            await store.DbContext.DeploymentResources.CountAsync(q => q.DeploymentFk == deployment.PrimaryKey));
        Assert.Equal("game-server", worker.ServiceIdentifier);
        Assert.Equal(0, worker.ServiceOrdinal);
        Assert.Equal(EWorkerStatus.Online, worker.Status);
        Assert.Equal(Now, worker.LastHeartbeatAt);
        Assert.Equal(4242, worker.ProcessId);
        Assert.Equal(Now - Duration.FromMinutes(90), worker.ProcessStartedAt);
        Assert.Null(worker.TimeStampStopped);
    }

    /// <summary>
    /// The seven day range returns 300 second points across the whole week without any point in the three hour stopped
    /// period two days ago.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_WeekHistoryHasNoPointInTheStoppedPeriod()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var history = await ReadHistoryAsync(store, seed, EServerMetricsRange.Week);

        var stoppedStart = Now - UatMetricHistorySeeder.StoppedPeriodAge;
        var stoppedEnd = stoppedStart + UatMetricHistorySeeder.StoppedPeriodLength;
        Assert.Equal(300, history.BucketSeconds);
        Assert.Equal(2016 - 36 - 2, history.Points.Count);
        Assert.DoesNotContain(history.Points, q => q.BucketStart >= stoppedStart && q.BucketStart < stoppedEnd);
        Assert.Contains(history.Points, q => q.BucketStart < stoppedStart);
        Assert.Contains(history.Points, q => q.BucketStart >= stoppedEnd);
        Assert.True(history.Points[0].BucketStart <= Now - Duration.FromDays(7) + Duration.FromMinutes(5));
        Assert.True(history.Points[^1].BucketStart >= Now - Duration.FromMinutes(10));
    }

    /// <summary>
    /// The 24 hour range returns 60 second points with the short worker outage 40 to 30 minutes ago left as a gap.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_DayHistoryShowsTheShortOutageGap()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var history = await ReadHistoryAsync(store, seed, EServerMetricsRange.Day);

        var outageStart = Now - UatMetricHistorySeeder.OutageAge;
        var outageEnd = outageStart + UatMetricHistorySeeder.OutageLength;
        Assert.Equal(60, history.BucketSeconds);
        Assert.Equal(1440 - 10, history.Points.Count);
        Assert.DoesNotContain(history.Points, q => q.BucketStart >= outageStart && q.BucketStart < outageEnd);
        Assert.Contains(history.Points, q => q.BucketStart < outageStart);
        Assert.Contains(history.Points, q => q.BucketStart >= outageEnd);
    }

    /// <summary>
    /// The one hour range returns 15 second points with the outage gap and the host totals.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_HourHistoryHasFifteenSecondPoints()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var history = await ReadHistoryAsync(store, seed, EServerMetricsRange.Hour);

        Assert.Equal(15, history.BucketSeconds);
        Assert.Equal(240 - 40, history.Points.Count);
        Assert.Equal(UatMetricHistorySeeder.MemoryTotalBytes, history.MemoryTotalBytes);
        Assert.Equal(UatMetricHistorySeeder.PlayerCapacity, history.PlayerCapacity);
    }

    /// <summary>
    /// The 60 second and 300 second windows five hours ago carry a CPU maximum of 95 while their averages stay below
    /// 60, so the spike survives downsampling without dragging the average.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_SpikeSurvivesDownsamplingWhileAveragesStayPlain()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        var day = await ReadHistoryAsync(store, seed, EServerMetricsRange.Day);
        var week = await ReadHistoryAsync(store, seed, EServerMetricsRange.Week);

        var spikeAt = Now - UatMetricHistorySeeder.SpikeAge;
        var minute = Assert.Single(day.Points, q => q.Cpu!.Maximum >= UatMetricHistorySeeder.SpikeCpuMaximum);
        var fiveMinutes = Assert.Single(week.Points, q => q.Cpu!.Maximum >= UatMetricHistorySeeder.SpikeCpuMaximum);
        Assert.Equal(ServerMetricResolutions.Floor(spikeAt, 60), minute.BucketStart);
        Assert.Equal(ServerMetricResolutions.Floor(spikeAt, 300), fiveMinutes.BucketStart);
        Assert.Equal(UatMetricHistorySeeder.SpikeCpuMaximum, minute.Cpu!.Maximum);
        Assert.Equal(UatMetricHistorySeeder.SpikeCpuMaximum, fiveMinutes.Cpu!.Maximum);
        Assert.InRange(minute.Cpu.Average, 0, 59.99);
        Assert.InRange(fiveMinutes.Cpu.Average, 0, 59.99);
    }

    /// <summary>
    /// Every seeded bucket has samples counted for every metric, an ordered minimum, average and maximum, memory below
    /// the total and players within the capacity.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_EverySeededBucketIsInternallyConsistent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedAsync(store);

        var buckets = await store.DbContext.ServerMetricBuckets.AsNoTracking().ToListAsync();

        Assert.NotEmpty(buckets);
        Assert.All(buckets, bucket =>
        {
            var samples = bucket.ResolutionSeconds / 15;
            Assert.Equal(samples, bucket.CpuCount);
            Assert.Equal(samples, bucket.MemoryCount);
            Assert.Equal(samples, bucket.PlayersCount);
            Assert.True(bucket.CpuMin <= bucket.CpuSum / bucket.CpuCount);
            Assert.True(bucket.CpuSum / bucket.CpuCount <= bucket.CpuMax);
            Assert.True(bucket.MemoryUsedMin <= (double) bucket.MemoryUsedSum / bucket.MemoryCount);
            Assert.True((double) bucket.MemoryUsedSum / bucket.MemoryCount <= bucket.MemoryUsedMax);
            Assert.True(bucket.MemoryUsedMax < bucket.MemoryTotalBytes);
            Assert.Equal(UatMetricHistorySeeder.MemoryTotalBytes, bucket.MemoryTotalBytes);
            Assert.InRange(bucket.PlayersMin!.Value, 0, 12);
            Assert.InRange(bucket.PlayersMax!.Value, 0, 12);
            Assert.True(bucket.PlayersMin <= (double) bucket.PlayersSum / bucket.PlayersCount);
            Assert.True((double) bucket.PlayersSum / bucket.PlayersCount <= bucket.PlayersMax);
            Assert.Equal(32, bucket.PlayerCapacity);
        });
    }

    /// <summary>
    /// Seeding twice creates no second metrics server, no second worker and no duplicate bucket.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MetricsScenario_SeedingTwiceCreatesNoDuplicates()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        var bucketCount = await store.DbContext.ServerMetricBuckets.CountAsync();

        await UatFleetScenarioSeeder.SeedAsync(
            store.DbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: Now);

        Assert.Equal(
            1,
            await store.DbContext.GameServers.CountAsync(q => q.Title == UatFleetScenarioSeeder.MetricsScenarioTitle));
        // The metrics scenario and the three running automation scenarios have exactly one primary worker each.
        Assert.Equal(4, await store.DbContext.LifecycleWorkers.CountAsync());
        Assert.Equal(bucketCount, await store.DbContext.ServerMetricBuckets.CountAsync());
        var distinctKeys = (await store.DbContext.ServerMetricBuckets
                .Select(q => new { q.GameServerFk, q.ResolutionSeconds, q.BucketStart })
                .ToListAsync())
            .Distinct()
            .Count();
        Assert.Equal(bucketCount, distinctKeys);
    }

    /// <summary>
    /// The idle, maximum runtime and scheduled stop scenarios are running possible-cost deployments with a resource and
    /// one online primary worker each, and the maximum runtime scenario started 70 minutes before seeding.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AutomationScenarios_CreateRunningDeploymentsWithOnlinePrimaryWorkers()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedAsync(store);

        foreach (var title in new[]
                 {
                     UatFleetScenarioSeeder.IdleScenarioTitle,
                     UatFleetScenarioSeeder.MaxRuntimeScenarioTitle,
                     UatFleetScenarioSeeder.ScheduledStopScenarioTitle,
                 })
        {
            var deployment = await store.DbContext.GameServerDeployments
                .Include(q => q.GameServer)
                .SingleAsync(q => q.GameServer!.Title == title);
            var worker = await store.DbContext.LifecycleWorkers.SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey);

            Assert.Equal(EDeploymentStatus.Running, deployment.Status);
            Assert.True(deployment.PossibleCost, title);
            Assert.Null(deployment.TimeStampResolved);
            Assert.Equal(
                1,
                await store.DbContext.DeploymentResources.CountAsync(q => q.DeploymentFk == deployment.PrimaryKey));
            Assert.Equal("game-server", worker.ServiceIdentifier);
            Assert.Equal(0, worker.ServiceOrdinal);
            Assert.Equal(EWorkerStatus.Online, worker.Status);
            Assert.Null(worker.TimeStampStopped);
            Assert.False(
                await store.DbContext.ServerAutomationSettings.AnyAsync(q => q.GameServerFk == deployment.GameServerFk),
                "The panel sets the automation of this scenario; it must start without settings.");
        }

        var aged = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.GameServer!.Title == UatFleetScenarioSeeder.MaxRuntimeScenarioTitle);
        var idle = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.GameServer!.Title == UatFleetScenarioSeeder.IdleScenarioTitle);
        Assert.Equal(Now - Duration.FromMinutes(70), aged.TimeStampStarted);
        Assert.Equal(Now, idle.TimeStampStarted);
    }

    /// <summary>
    /// The emulated worker map drives the three running automation scenarios with the zero-player and few-player feeds.
    /// </summary>
    [Fact]
    public void EmulatedWorkers_DriveTheAutomationScenariosWithTheirPlayerProfiles()
    {
        var workers = UatFleetScenarioSeeder.EmulatedWorkers;

        Assert.Equal(EUatPlayerProfile.NoPlayers, workers[UatFleetScenarioSeeder.IdleScenarioTitle]);
        Assert.Equal(EUatPlayerProfile.FewPlayers, workers[UatFleetScenarioSeeder.MaxRuntimeScenarioTitle]);
        Assert.Equal(EUatPlayerProfile.FewPlayers, workers[UatFleetScenarioSeeder.ScheduledStopScenarioTitle]);
        Assert.Equal(EUatPlayerProfile.Synthetic, workers[UatFleetScenarioSeeder.MetricsScenarioTitle]);
    }

    /// <summary>
    /// The three scheduled start scenarios have no deployment; the start scenario has no settings, the skipped scenario
    /// is billed to the unfunded owner with a daily rule three minutes after seeding and a watermark at the seed
    /// instant, and the paused scenario has a Sunday rule with consent recorded for another cost owner.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StartScenarios_CarryTheirSettingsRulesAndConsent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);
        var db = store.DbContext;
        var unfunded = await db.CostOwners.SingleAsync(q => q.DisplayName == UatFleetScenarioSeeder.UnfundedCostOwnerName);
        var start = await db.GameServers.SingleAsync(q => q.Title == UatFleetScenarioSeeder.ScheduledStartScenarioTitle);
        var skipped = await db.GameServers.SingleAsync(q => q.Title == UatFleetScenarioSeeder.StartSkippedScenarioTitle);
        var paused = await db.GameServers.SingleAsync(q => q.Title == UatFleetScenarioSeeder.StartsPausedScenarioTitle);

        Assert.Equal(0m, unfunded.Balance);
        Assert.Equal(ECostOwnerKind.ExternalOrganization, unfunded.Kind);
        Assert.Empty(
            await db.GameServerDeployments
                .Where(q => q.GameServerFk == start.PrimaryKey
                            || q.GameServerFk == skipped.PrimaryKey
                            || q.GameServerFk == paused.PrimaryKey)
                .ToListAsync());
        Assert.False(await db.ServerAutomationSettings.AnyAsync(q => q.GameServerFk == start.PrimaryKey));
        Assert.Equal(seed.CostOwner.PrimaryKey, start.CurrentCostOwnerFk);
        Assert.Equal(unfunded.PrimaryKey, skipped.CurrentCostOwnerFk);
        Assert.Equal(seed.CostOwner.PrimaryKey, paused.CurrentCostOwnerFk);

        var skippedSettings = await db.ServerAutomationSettings.SingleAsync(q => q.GameServerFk == skipped.PrimaryKey);
        var skippedRule = await db.ServerSchedules.SingleAsync(q => q.GameServerFk == skipped.PrimaryKey);
        var skippedState = await db.ServerAutomationStates.SingleAsync(q => q.GameServerFk == skipped.PrimaryKey);
        Assert.Equal(unfunded.PrimaryKey, skippedSettings.StartConsentCostOwnerFk);
        Assert.NotNull(skippedSettings.StartConsentAt);
        Assert.Equal("UTC", skippedSettings.TimeZone);
        Assert.Equal(EServerScheduleAction.Start, skippedRule.Action);
        Assert.Equal(ScheduleCalculator.ToDaysMask([1, 2, 3, 4, 5, 6, 7]), skippedRule.DaysMask);
        Assert.Equal(8 * 60 + 3, skippedRule.MinuteOfDay);
        Assert.Equal(Now, skippedRule.EffectiveFrom);
        Assert.Equal(Now, skippedState.SchedulesEvaluatedThrough);

        var pausedSettings = await db.ServerAutomationSettings.SingleAsync(q => q.GameServerFk == paused.PrimaryKey);
        var pausedRule = await db.ServerSchedules.SingleAsync(q => q.GameServerFk == paused.PrimaryKey);
        Assert.Equal(unfunded.PrimaryKey, pausedSettings.StartConsentCostOwnerFk);
        Assert.NotEqual(paused.CurrentCostOwnerFk, pausedSettings.StartConsentCostOwnerFk);
        Assert.Equal(ScheduleCalculator.ToDaysMask([7]), pausedRule.DaysMask);
        Assert.Equal(3 * 60, pausedRule.MinuteOfDay);
        Assert.Equal(Now, pausedRule.EffectiveFrom);
    }

    /// <summary>
    /// An evaluator pass four minutes after seeding records the occurrence of the skipped scenario as skipped for cost
    /// and queues no start, while the paused scenario reports that its start consent is needed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StartScenarios_AfterFourMinutes_SkipForCostAndReportConsentNeeded()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedAsync(store);
        var db = store.DbContext;
        var skipped = await db.GameServers.SingleAsync(q => q.Title == UatFleetScenarioSeeder.StartSkippedScenarioTitle);

        var result = await ServerAutomationTestData.CreateEvaluator(db)
            .EvaluateAsync(Now + Duration.FromMinutes(4), CancellationToken.None);

        db.ChangeTracker.Clear();
        Assert.Equal(0, result.JobsQueued);
        var run = Assert.Single(await db.ServerScheduleRuns.ToListAsync());
        Assert.Equal(skipped.PrimaryKey, run.GameServerFk);
        Assert.Equal(EServerScheduleRunOutcome.SkippedCost, run.Outcome);
        Assert.Empty(await db.LifecycleJobs.ToListAsync());

        var paused = await db.GameServers
            .Include(q => q.Deployments)
            .Include(q => q.LifecycleJobs)
            .SingleAsync(q => q.Title == UatFleetScenarioSeeder.StartsPausedScenarioTitle);
        // The projector resolves the game definition of the test catalog, not the production Arma 3 identifier.
        paused.GameIdentifier = HostingTestData.GameIdentifier;
        var projection = await ServerAutomationTestData.CreateProjector(db)
            .ProjectAsync(paused, Now + Duration.FromMinutes(4), CancellationToken.None);
        Assert.NotNull(projection);
        Assert.Equal(EStartConsentState.Needed, projection.StartConsent.State);
    }

    /// <summary>
    /// Seeding twice creates no second automation server, no second settings row, no second rule and no second
    /// unfunded cost owner.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AutomationScenarios_SeedingTwiceCreatesNoDuplicates()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAsync(store);

        await UatFleetScenarioSeeder.SeedAsync(
            store.DbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: Now + Duration.FromMinutes(1));

        var db = store.DbContext;
        Assert.Equal(16, await db.GameServers.CountAsync(q => q.Title.StartsWith("UAT ")));
        Assert.Equal(2, await db.ServerAutomationSettings.CountAsync());
        Assert.Equal(2, await db.ServerSchedules.CountAsync());
        Assert.Equal(2, await db.ServerAutomationStates.CountAsync());
        Assert.Equal(
            1,
            await db.CostOwners.CountAsync(q => q.DisplayName == UatFleetScenarioSeeder.UnfundedCostOwnerName));
    }

    /// <summary>
    /// Reads the history of the seeded metrics server for one range at <see cref="Now"/> as an authorized viewer.
    /// </summary>
    /// <param name="store">The store that holds the seeded data.</param>
    /// <param name="seed">The operator seed that owns the UAT servers.</param>
    /// <param name="range">The range to read.</param>
    /// <returns>A task resolving to the history the metrics service returns.</returns>
    private static async Task<GameServerMetricsHistoryDto> ReadHistoryAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        EServerMetricsRange range)
    {
        var server = await store.DbContext.GameServers
            .SingleAsync(q => q.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        var viewer = HostingTestData.ServerPrincipal(seed.User, server, Claims.ResourceBased.Server.View);
        return await ServerMetricsTestData.Create(store.DbContext)
            .GetHistoryAsync(server.PrimaryKey, viewer, range, Now, CancellationToken.None);
    }

    /// <summary>
    /// Creates a hosting operator with a personal cost owner and runs the UAT seeder against it at <see cref="Now"/>.
    /// </summary>
    /// <param name="store">The store whose database is seeded.</param>
    /// <returns>A task resolving to the operator seed that owns the UAT servers.</returns>
    private static async Task<HostingTestSeed> SeedAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await UatFleetScenarioSeeder.SeedAsync(
            store.DbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: Now);
        return seed;
    }

    /// <summary>
    /// Loads the deployment of the seeded costing server.
    /// </summary>
    /// <param name="store">The store that holds the seeded data.</param>
    /// <returns>A task resolving to the tracked costing deployment.</returns>
    private static async Task<GameServerDeployment> LoadCostingDeploymentAsync(HostingTestStore store)
    {
        return await store.DbContext.GameServerDeployments
            .Include(q => q.GameServer)
            .SingleAsync(q => q.GameServer!.Title == UatFleetScenarioSeeder.CostingScenarioTitle);
    }
}
