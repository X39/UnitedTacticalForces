using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-04 proof that a threshold crossing becomes a durable, once-only record with a delivery intent per channel and
/// recipient, created in one transaction. The tests run the real <see cref="CostAlertEvaluator" />,
/// <see cref="CostAccrualService" /> and <see cref="CostAlertRecipientResolver" /> against in-memory SQLite.
/// </summary>
public sealed class CostAlertEvaluatorTests
{
    /// <summary>
    /// The instant every seeded deployment starts at.
    /// </summary>
    private static readonly Instant Start = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// The instant at which the seeded 15-minute slice is fully elapsed.
    /// </summary>
    private static readonly Instant SliceEnd = Start + Duration.FromMinutes(15);

    /// <summary>
    /// A costing session that reached one of two thresholds records exactly one alert with a pending delivery per
    /// channel for the owner user.
    /// </summary>
    /// <returns>A task completing after the persisted rows were verified.</returns>
    [Fact]
    public async Task CostingSession_ReachingThreshold_RecordsOneAlertWithDeliveries()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedCostingSessionAsync(store, 3.0m, discordId: 4711, email: "operator@example.org", 1m, 5m);

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        var alert = Assert.Single(await store.DbContext.CostAlerts.AsNoTracking().ToListAsync());
        Assert.Equal(seed.Deployment!.PrimaryKey, alert.DeploymentFk);
        Assert.Equal(seed.Server.PrimaryKey, alert.GameServerFk);
        Assert.Equal(seed.CostOwner.PrimaryKey, alert.CostOwnerFk);
        Assert.Equal(1m, alert.ThresholdAmount);
        Assert.Equal(3.0000m, alert.AccruedAmount);
        Assert.Equal("EUR", alert.Currency);
        Assert.Equal(SliceEnd, alert.TimeStampCreated);
        var deliveries = await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, q =>
        {
            Assert.Equal(alert.PrimaryKey, q.CostAlertFk);
            Assert.Equal(seed.User.PrimaryKey, q.RecipientUserFk);
            Assert.Equal(ECostAlertDeliveryStatus.Pending, q.Status);
            Assert.Null(q.StatusReason);
            Assert.Equal(SliceEnd, q.TimeStampNextAttempt);
        });
        Assert.Equal(
            [ECostAlertChannel.Discord, ECostAlertChannel.Email],
            deliveries.Select(q => q.Channel).OrderBy(q => q).ToArray());

        var accrual = Assert.Single(result.Accruals);
        Assert.True(accrual.IsCosting);
        Assert.Equal(seed.Deployment.PrimaryKey, accrual.DeploymentId);
        Assert.Equal(3.0000m, accrual.Result.Amount);
        var raised = Assert.Single(result.RaisedAlerts);
        Assert.Equal(alert.PrimaryKey, raised.CostAlertId);
        Assert.Equal("Integration server", raised.ServerTitle);
        Assert.Equal(1m, raised.ThresholdAmount);
    }

    /// <summary>
    /// Thresholds that are all crossed in one pass create one alert each, in ascending threshold order.
    /// </summary>
    /// <returns>A task completing after the three alerts were verified.</returns>
    [Fact]
    public async Task SeveralThresholdsCrossedInOnePass_CreateOneAlertEach()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org", 1m, 2m, 2.5m, 4m);

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        Assert.Equal([1m, 2m, 2.5m], result.RaisedAlerts.Select(q => q.ThresholdAmount).ToArray());
        var alerts = await store.DbContext.CostAlerts.AsNoTracking().ToListAsync();
        Assert.Equal(3, alerts.Count);
        Assert.Equal(6, await store.DbContext.CostAlertDeliveries.CountAsync());
    }

    /// <summary>
    /// An owner without a setting row never alerts (D-05).
    /// </summary>
    /// <returns>A task completing after the absence of alerts was verified.</returns>
    [Fact]
    public async Task OwnerWithoutSetting_NeverAlerts()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(store.DbContext, seed.Deployment!, Start, SliceEnd, 3.0m);

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        Assert.Empty(result.RaisedAlerts);
        Assert.Single(result.Accruals);
        Assert.Empty(await store.DbContext.CostAlerts.ToListAsync());
    }

    /// <summary>
    /// An owner with a setting but an empty threshold list never alerts (D-05).
    /// </summary>
    /// <returns>A task completing after the absence of alerts was verified.</returns>
    [Fact]
    public async Task OwnerWithEmptyThresholdList_NeverAlerts()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org");

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        Assert.Empty(result.RaisedAlerts);
        Assert.Empty(await store.DbContext.CostAlerts.ToListAsync());
    }

    /// <summary>
    /// A threshold at or below the current accrued cost counts as reached, while a higher one does not.
    /// </summary>
    /// <returns>A task completing after the boundary was verified.</returns>
    [Fact]
    public async Task ThresholdEqualToAccruedAmount_CountsAsReached()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org", 3m, 3.01m);

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        var raised = Assert.Single(result.RaisedAlerts);
        Assert.Equal(3m, raised.ThresholdAmount);
    }

    /// <summary>
    /// Skipped delivery rows carry a reason code and never suppress the alert itself (D-09).
    /// </summary>
    /// <param name="discordEnabled">Whether the owner's setting enables Discord.</param>
    /// <param name="emailEnabled">Whether the owner's setting enables email.</param>
    /// <param name="discordId">The recipient's Discord id; zero means not linked.</param>
    /// <param name="email">The recipient's email address.</param>
    /// <param name="discordReason">The expected Discord reason, or <see langword="null"/> when pending.</param>
    /// <param name="emailReason">The expected email reason, or <see langword="null"/> when pending.</param>
    /// <returns>A task completing after the delivery rows were verified.</returns>
    [Theory]
    [InlineData(true, true, 7UL, "a@example.org", null, null)]
    [InlineData(false, true, 7UL, "a@example.org", "channel-disabled", null)]
    [InlineData(true, false, 7UL, "a@example.org", null, "channel-disabled")]
    [InlineData(true, true, 0UL, "a@example.org", "not-linked", null)]
    [InlineData(true, true, 7UL, null, null, "no-address")]
    [InlineData(true, true, 7UL, "  ", null, "no-address")]
    [InlineData(false, false, 0UL, null, "channel-disabled", "channel-disabled")]
    public async Task DeliveryRows_FollowChannelFlagsAndRecipientReachability(
        bool discordEnabled,
        bool emailEnabled,
        ulong discordId,
        string? email,
        string? discordReason,
        string? emailReason)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedCostingSessionAsync(
            store, 3.0m, (long) discordId, email, discordEnabled, emailEnabled, 1m);

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        Assert.Single(result.RaisedAlerts);
        var deliveries = await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, deliveries.Count);
        AssertDelivery(deliveries.Single(q => q.Channel == ECostAlertChannel.Discord), discordReason);
        AssertDelivery(deliveries.Single(q => q.Channel == ECostAlertChannel.Email), emailReason);
        Assert.All(deliveries, q => Assert.Equal(seed.User.PrimaryKey, q.RecipientUserFk));
    }

    /// <summary>
    /// A deployment resolved within the lookback is alerted for its final amount, one resolved before the lookback
    /// is ignored, and a costing deployment without charged slices is ignored.
    /// </summary>
    /// <returns>A task completing after the three candidate classes were verified.</returns>
    [Fact]
    public async Task ResolvedWithinLookbackIsAlerted_OlderResolvedAndUnchargedAreIgnored()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org", 1m);
        var deployment = await store.DbContext.GameServerDeployments.SingleAsync();
        deployment.PossibleCost = false;
        deployment.TimeStampResolved = SliceEnd;
        await store.DbContext.SaveChangesAsync();

        var ignored = await CreateEvaluator(store).EvaluateAsync(
            SliceEnd + Duration.FromHours(25), CancellationToken.None);
        Assert.Empty(ignored.Accruals);
        Assert.Empty(ignored.RaisedAlerts);

        var alerted = await CreateEvaluator(store).EvaluateAsync(
            SliceEnd + Duration.FromHours(1), CancellationToken.None);
        var accrual = Assert.Single(alerted.Accruals);
        Assert.False(accrual.IsCosting);
        Assert.Equal(3.0000m, accrual.Result.Amount);
        Assert.Single(alerted.RaisedAlerts);

        // A costing session that has no charged slice yet has an unknown amount and never alerts.
        var secondServer = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.SetCostAlertThresholdsAsync(
            store.DbContext, secondServer.CostOwner.PrimaryKey, true, true, 0.01m);
        var uncharged = await CreateEvaluator(store).EvaluateAsync(
            SliceEnd + Duration.FromHours(1), CancellationToken.None);
        Assert.Contains(uncharged.Accruals, q => q.DeploymentId == secondServer.Deployment!.PrimaryKey);
        Assert.Empty(uncharged.RaisedAlerts);
        var recorded = Assert.Single(await store.DbContext.CostAlerts.ToListAsync());
        Assert.Equal(seed.Deployment!.PrimaryKey, recorded.DeploymentFk);
    }

    /// <summary>
    /// Evaluating again with fresh service instances on a new context over the same database, as after an API
    /// restart, records nothing new.
    /// </summary>
    /// <returns>A task completing after the unchanged row counts were verified.</returns>
    [Fact]
    public async Task EvaluatingAgainAfterRestart_RecordsNothingNew()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org", 1m, 2m);
        var first = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);
        Assert.Equal(2, first.RaisedAlerts.Count);

        await using var restarted = new NodaTimeSqliteApiDbContext(
            new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(store.Connection).Options);
        var second = await CreateEvaluator(restarted).EvaluateAsync(
            SliceEnd + Duration.FromMinutes(1), CancellationToken.None);
        var duplicatePass = await CreateEvaluator(restarted).EvaluateAsync(
            SliceEnd + Duration.FromMinutes(2), CancellationToken.None);

        Assert.Empty(second.RaisedAlerts);
        Assert.Empty(duplicatePass.RaisedAlerts);
        Assert.Single(second.Accruals);
        Assert.Equal(2, await restarted.CostAlerts.CountAsync());
        Assert.Equal(4, await restarted.CostAlertDeliveries.CountAsync());
    }

    /// <summary>
    /// A concurrent pass that commits the same crossing between this pass's read and its insert makes the unique
    /// index reject the insert, which is reported as nothing raised instead of failing, and records no duplicate.
    /// </summary>
    /// <returns>A task completing after the single surviving alert was verified.</returns>
    [Fact]
    public async Task CrossingCommittedByConcurrentPass_IsNotRecordedTwice()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org", 1m);
        var evaluator = new CostAlertEvaluator(
            store.DbContext,
            new CostAccrualService(
                store.DbContext,
                new HostingAuthorizationService(store.DbContext),
                CreateRuntimeProviders()),
            new CommitCrossingBeforeResolveResolver(store.DbContext, seed, SliceEnd),
            Options.Create(new CostAlertOptions()),
            NullLogger<CostAlertEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(SliceEnd, CancellationToken.None);

        Assert.Empty(result.RaisedAlerts);
        Assert.Single(result.Accruals);
        Assert.Equal(1, await store.DbContext.CostAlerts.CountAsync());
        Assert.Equal(0, await store.DbContext.CostAlertDeliveries.CountAsync());
    }

    /// <summary>
    /// A second deployment of the same server that reaches the same threshold records a new alert, because a new
    /// session starts with fresh crossings.
    /// </summary>
    /// <returns>A task completing after both sessions' alerts were verified.</returns>
    [Fact]
    public async Task NewSessionOfSameServer_RecordsFreshCrossing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedCostingSessionAsync(store, 3.0m, discordId: 1, email: "a@example.org", 1m);
        var first = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);
        Assert.Single(first.RaisedAlerts);

        var firstDeployment = await store.DbContext.GameServerDeployments.SingleAsync();
        firstDeployment.PossibleCost = false;
        firstDeployment.TimeStampResolved = SliceEnd;
        var secondStart = SliceEnd + Duration.FromHours(1);
        var secondDeployment = AddDeployment(store, seed, seed.CostOwner.PrimaryKey, secondStart);
        await store.DbContext.SaveChangesAsync();
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, secondDeployment, secondStart, secondStart + Duration.FromMinutes(15), 3.0m);

        var second = await CreateEvaluator(store).EvaluateAsync(
            secondStart + Duration.FromMinutes(15), CancellationToken.None);

        var raised = Assert.Single(second.RaisedAlerts);
        Assert.Equal(1m, raised.ThresholdAmount);
        var alerts = await store.DbContext.CostAlerts.AsNoTracking().ToListAsync();
        Assert.Equal(2, alerts.Count);
        Assert.Equal(
            new[] { firstDeployment.PrimaryKey, secondDeployment.PrimaryKey }.Order().ToArray(),
            alerts.Select(q => q.DeploymentFk).Order().ToArray());
    }

    /// <summary>
    /// An external organization owner without any co:use holder still gets the alert, with one skipped delivery row
    /// per channel that has no recipient.
    /// </summary>
    /// <returns>A task completing after the skipped rows were verified.</returns>
    [Fact]
    public async Task OwnerWithoutRecipient_StoresOneSkippedRowPerChannel()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var sponsor = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.ExternalOrganization,
            DisplayName = "Sponsor",
            Currency = "EUR",
            TimeStampCreated = Start,
            TimeStampUpdated = Start,
        };
        store.DbContext.CostOwners.Add(sponsor);
        var deployment = AddDeployment(store, seed, sponsor.PrimaryKey, Start);
        await store.DbContext.SaveChangesAsync();
        await HostingTestData.AddCostSliceAsync(store.DbContext, deployment, Start, SliceEnd, 3.0m);
        await HostingTestData.SetCostAlertThresholdsAsync(store.DbContext, sponsor.PrimaryKey, true, true, 1m);

        var result = await CreateEvaluator(store).EvaluateAsync(SliceEnd, CancellationToken.None);

        Assert.Single(result.RaisedAlerts);
        var deliveries = await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, q =>
        {
            Assert.Null(q.RecipientUserFk);
            Assert.Equal(ECostAlertDeliveryStatus.Skipped, q.Status);
            Assert.Equal("no-recipient", q.StatusReason);
        });
        Assert.Equal(
            [ECostAlertChannel.Discord, ECostAlertChannel.Email],
            deliveries.Select(q => q.Channel).OrderBy(q => q).ToArray());
    }

    /// <summary>
    /// Adds an unsaved running deployment of a seeded server for the given cost owner.
    /// </summary>
    /// <param name="store">The test store whose context tracks the deployment.</param>
    /// <param name="seed">The seeded server the deployment belongs to; it must have no unresolved deployment.</param>
    /// <param name="costOwnerId">The cost owner the deployment is billed to.</param>
    /// <param name="startedAt">The instant the deployment started at.</param>
    /// <returns>The added deployment.</returns>
    private static GameServerDeployment AddDeployment(
        HostingTestStore store,
        HostingTestSeed seed,
        Guid costOwnerId,
        Instant startedAt)
    {
        var deployment = new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = costOwnerId,
            Status = EDeploymentStatus.Running,
            Stage = "running",
            PossibleCost = true,
            TimeStampCreated = startedAt,
            TimeStampUpdated = startedAt,
            TimeStampStarted = startedAt,
        };
        store.DbContext.GameServerDeployments.Add(deployment);
        return deployment;
    }

    /// <summary>
    /// Verifies the status and reason of one delivery row.
    /// </summary>
    /// <param name="delivery">The persisted delivery row.</param>
    /// <param name="skipReason">The expected skip reason, or <see langword="null"/> when pending is expected.</param>
    private static void AssertDelivery(CostAlertDelivery delivery, string? skipReason)
    {
        if (skipReason is null)
        {
            Assert.Equal(ECostAlertDeliveryStatus.Pending, delivery.Status);
            Assert.Null(delivery.StatusReason);
            Assert.NotNull(delivery.TimeStampNextAttempt);
            return;
        }

        Assert.Equal(ECostAlertDeliveryStatus.Skipped, delivery.Status);
        Assert.Equal(skipReason, delivery.StatusReason);
        Assert.Null(delivery.TimeStampNextAttempt);
    }

    /// <summary>
    /// A Hetzner session bills every started hour, so its 0.10 EUR threshold is crossed one minute into the first
    /// slice and recorded with the full-hour accrued amount.
    /// </summary>
    /// <returns>A task completing after the alert was verified.</returns>
    [Fact]
    public async Task HetznerSession_CrossesThresholdAtMinuteOne()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(
            store, 0.0250m, 1, "a@example.org", HetznerRuntimeProvider.Identifier, true, true, 0.10m);
        var minuteOne = Start + Duration.FromMinutes(1);

        var first = await CreateEvaluator(store).EvaluateAsync(minuteOne, CancellationToken.None);

        var alert = Assert.Single(await store.DbContext.CostAlerts.AsNoTracking().ToListAsync());
        Assert.Equal(0.10m, alert.ThresholdAmount);
        Assert.Equal(0.1000m, alert.AccruedAmount);
        Assert.Equal(minuteOne, alert.TimeStampCreated);
        Assert.Equal(2, await store.DbContext.CostAlertDeliveries.CountAsync());
        var raised = Assert.Single(first.RaisedAlerts);
        Assert.Equal(alert.PrimaryKey, raised.CostAlertId);
    }

    /// <summary>
    /// A later pass over the same Hetzner session records nothing new: a threshold alerts once per deployment.
    /// </summary>
    /// <returns>A task completing after the unchanged alert count was verified.</returns>
    [Fact]
    public async Task HetznerSession_LaterPassAtMinuteTwo_RecordsNothingNew()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(
            store, 0.0250m, 1, "a@example.org", HetznerRuntimeProvider.Identifier, true, true, 0.10m);
        await CreateEvaluator(store).EvaluateAsync(Start + Duration.FromMinutes(1), CancellationToken.None);

        var second = await CreateEvaluator(store).EvaluateAsync(
            Start + Duration.FromMinutes(2), CancellationToken.None);

        Assert.Empty(second.RaisedAlerts);
        Assert.Equal(1, await store.DbContext.CostAlerts.CountAsync());
    }

    /// <summary>
    /// The same session on a per-started-second provider has cost almost nothing at minute one, so the threshold is
    /// not crossed.
    /// </summary>
    /// <returns>A task completing after the absence of an alert was verified.</returns>
    [Fact]
    public async Task PerSecondProviderSession_DoesNotCrossThresholdAtMinuteOne()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await SeedCostingSessionAsync(
            store, 0.0250m, 1, "a@example.org", TestRuntimeProvider.Identifier, true, true, 0.10m);

        var result = await CreateEvaluator(store).EvaluateAsync(
            Start + Duration.FromMinutes(1), CancellationToken.None);

        Assert.Empty(result.RaisedAlerts);
        Assert.Empty(await store.DbContext.CostAlerts.ToListAsync());
        Assert.Equal(0.0017m, Assert.Single(result.Accruals).Result.Amount);
    }

    /// <summary>
    /// The accrual the evaluator uses for a threshold equals what the accrued-cost endpoint reports at that instant.
    /// </summary>
    /// <returns>A task completing after both values were compared.</returns>
    [Fact]
    public async Task HetznerSession_EvaluatorAccrualEqualsTheEndpointAmount()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedCostingSessionAsync(
            store, 0.0250m, 1, "a@example.org", HetznerRuntimeProvider.Identifier, true, true, 0.10m);
        var minuteOne = Start + Duration.FromMinutes(1);

        var result = await CreateEvaluator(store).EvaluateAsync(minuteOne, CancellationToken.None);
        var dto = await new CostAccrualService(
                store.DbContext,
                new HostingAuthorizationService(store.DbContext),
                CreateRuntimeProviders())
            .GetForServerAsync(
                seed.Server.PrimaryKey,
                HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
                minuteOne,
                CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(0.1000m, session.Amount);
        Assert.Equal(session.Amount, Assert.Single(result.Accruals).Result.Amount);
    }

    /// <summary>
    /// Seeds a server with an active deployment whose 15-minute slice charged <paramref name="sliceAmount" />, sets
    /// the owner user's reachability and stores the owner's thresholds.
    /// </summary>
    /// <param name="store">The test store to seed.</param>
    /// <param name="sliceAmount">The amount charged for the whole slice.</param>
    /// <param name="discordId">The owner user's Discord id; zero means not linked.</param>
    /// <param name="email">The owner user's email address.</param>
    /// <param name="amounts">The threshold amounts of the owner.</param>
    /// <returns>A task resolving to the seeded entities.</returns>
    private static Task<HostingTestSeed> SeedCostingSessionAsync(
        HostingTestStore store,
        decimal sliceAmount,
        long discordId,
        string? email,
        params decimal[] amounts)
        => SeedCostingSessionAsync(store, sliceAmount, discordId, email, true, true, amounts);

    /// <summary>
    /// Seeds a server with an active deployment whose 15-minute slice charged <paramref name="sliceAmount" />, sets
    /// the owner user's reachability and stores the owner's channel flags and thresholds.
    /// </summary>
    /// <param name="store">The test store to seed.</param>
    /// <param name="sliceAmount">The amount charged for the whole slice.</param>
    /// <param name="discordId">The owner user's Discord id; zero means not linked.</param>
    /// <param name="email">The owner user's email address.</param>
    /// <param name="discordEnabled">Whether the owner's setting enables Discord.</param>
    /// <param name="emailEnabled">Whether the owner's setting enables email.</param>
    /// <param name="amounts">The threshold amounts of the owner.</param>
    /// <returns>A task resolving to the seeded entities.</returns>
    private static Task<HostingTestSeed> SeedCostingSessionAsync(
        HostingTestStore store,
        decimal sliceAmount,
        long discordId,
        string? email,
        bool discordEnabled,
        bool emailEnabled,
        params decimal[] amounts)
        => SeedCostingSessionAsync(
            store, sliceAmount, discordId, email, null, discordEnabled, emailEnabled, amounts);

    /// <summary>
    /// Seeds a server with an active deployment whose 15-minute slice was charged by the given provider,
    /// sets the owner user's reachability and stores the owner's channel flags and thresholds.
    /// </summary>
    /// <param name="store">The test store to seed.</param>
    /// <param name="sliceAmount">The amount charged for the whole slice.</param>
    /// <param name="discordId">The owner user's Discord id; zero means not linked.</param>
    /// <param name="email">The owner user's email address.</param>
    /// <param name="providerIdentifier">
    /// The provider identifier recorded on the ledger entry, or <see langword="null"/> for the test provider.
    /// </param>
    /// <param name="discordEnabled">Whether the owner's setting enables Discord.</param>
    /// <param name="emailEnabled">Whether the owner's setting enables email.</param>
    /// <param name="amounts">The threshold amounts of the owner.</param>
    /// <returns>A task resolving to the seeded entities.</returns>
    private static async Task<HostingTestSeed> SeedCostingSessionAsync(
        HostingTestStore store,
        decimal sliceAmount,
        long discordId,
        string? email,
        string? providerIdentifier,
        bool discordEnabled,
        bool emailEnabled,
        params decimal[] amounts)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.User.Discord.Id = (ulong) discordId;
        seed.User.EMail = email;
        await store.DbContext.SaveChangesAsync();
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, SliceEnd, sliceAmount, providerIdentifier);
        await HostingTestData.SetCostAlertThresholdsAsync(
            store.DbContext, seed.CostOwner.PrimaryKey, discordEnabled, emailEnabled, amounts);
        return seed;
    }

    /// <summary>
    /// A recipient resolver that first commits the crossing the evaluator is about to record, standing in for a
    /// concurrent evaluation pass that wins the race, and then resolves nobody.
    /// </summary>
    /// <param name="dbContext">The context the competing crossing is committed with.</param>
    /// <param name="seed">The seeded session whose threshold of 1 is committed.</param>
    /// <param name="createdAt">The instant recorded on the competing alert.</param>
    private sealed class CommitCrossingBeforeResolveResolver(
        DatabaseContext dbContext,
        HostingTestSeed seed,
        Instant createdAt) : ICostAlertRecipientResolver
    {
        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CostAlertRecipient>>> ResolveAsync(
            IReadOnlyCollection<Guid> costOwnerIds,
            CancellationToken cancellationToken)
        {
            dbContext.CostAlerts.Add(new CostAlert
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = seed.Deployment!.PrimaryKey,
                GameServerFk = seed.Server.PrimaryKey,
                CostOwnerFk = seed.CostOwner.PrimaryKey,
                ThresholdAmount = 1m,
                AccruedAmount = 3m,
                TimeStampCreated = createdAt,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return costOwnerIds.ToDictionary(q => q, _ => (IReadOnlyList<CostAlertRecipient>) []);
        }
    }

    /// <summary>
    /// Creates the evaluator under test on the store's database context.
    /// </summary>
    /// <param name="store">The test store whose context the evaluator reads and writes.</param>
    /// <returns>The evaluator with real accrual and recipient services.</returns>
    private static CostAlertEvaluator CreateEvaluator(HostingTestStore store)
        => CreateEvaluator(store.DbContext);

    /// <summary>
    /// Creates the runtime providers the accrual service resolves billing units from.
    /// </summary>
    /// <returns>The per-second test provider and the started-hour emulated Hetzner provider.</returns>
    private static IRuntimeProvider[] CreateRuntimeProviders()
        => [new TestRuntimeProvider(), new EmulatedHetznerRuntimeProvider()];

    /// <summary>
    /// Creates the evaluator under test on an arbitrary database context, for example a second context that stands
    /// for a restarted API instance.
    /// </summary>
    /// <param name="dbContext">The database context the evaluator reads and writes.</param>
    /// <returns>The evaluator with real accrual and recipient services.</returns>
    private static CostAlertEvaluator CreateEvaluator(DatabaseContext dbContext)
        => new(
            dbContext,
            new CostAccrualService(
                dbContext,
                new HostingAuthorizationService(dbContext),
                CreateRuntimeProviders()),
            new CostAlertRecipientResolver(dbContext),
            Options.Create(new CostAlertOptions()),
            NullLogger<CostAlertEvaluator>.Instance);
}
