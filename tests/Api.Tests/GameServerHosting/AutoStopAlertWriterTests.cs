using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// AUT-04 proof that a pending automatic stop becomes an <see cref="ECostAlertKind.AutoStop" /> cost alert with the
/// Phase 66 delivery intents, that the alert list shows the stop facts, that resolving the pending stop withdraws the
/// alert from every undelivered channel and the list, and that threshold alerts are unaffected.
/// </summary>
public sealed class AutoStopAlertWriterTests
{
    /// <summary>
    /// The instant the seeded warnings are created at.
    /// </summary>
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// The instant the seeded stops follow at.
    /// </summary>
    private static readonly Instant StopsAt = Now + Duration.FromMinutes(5);

    /// <summary>
    /// With Discord off and email on, one AutoStop alert is added with a pending email row and a skipped Discord row,
    /// and the raised record carries the stop facts.
    /// </summary>
    /// <returns>A task completing after the added rows were verified.</returns>
    [Fact]
    public async Task AddWarning_DiscordOffEmailOn_AddsAlertWithPendingEmailAndSkippedDiscord()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedReachableOwnerAsync(store, discordEnabled: false, emailEnabled: true);
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.Idle, 30);

        var raised = await CreateWriter(store).AddWarningAsync(
            pending,
            seed.Deployment!,
            "Integration server",
            Now,
            CancellationToken.None);
        await store.DbContext.SaveChangesAsync();

        var alert = Assert.Single(await store.DbContext.CostAlerts.AsNoTracking().ToListAsync());
        Assert.Equal(ECostAlertKind.AutoStop, alert.Kind);
        Assert.Equal(pending.PrimaryKey, alert.PendingStopFk);
        Assert.Equal(seed.Deployment!.PrimaryKey, alert.DeploymentFk);
        Assert.Equal(seed.Server.PrimaryKey, alert.GameServerFk);
        Assert.Equal(seed.CostOwner.PrimaryKey, alert.CostOwnerFk);
        Assert.Equal(0m, alert.ThresholdAmount);
        Assert.Equal(0m, alert.AccruedAmount);
        Assert.Equal(Now, alert.TimeStampCreated);
        var deliveries = await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync();
        var email = Assert.Single(deliveries, q => q.Channel == ECostAlertChannel.Email);
        Assert.Equal(ECostAlertDeliveryStatus.Pending, email.Status);
        Assert.Equal(seed.User.PrimaryKey, email.RecipientUserFk);
        Assert.Equal(Now, email.TimeStampNextAttempt);
        var discord = Assert.Single(deliveries, q => q.Channel == ECostAlertChannel.Discord);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, discord.Status);
        Assert.Equal(CostAlertDeliveryReasons.ChannelDisabled, discord.StatusReason);

        Assert.Equal(alert.PrimaryKey, raised.CostAlertId);
        Assert.Equal(ECostAlertKind.AutoStop, raised.Kind);
        Assert.Equal(StopsAt, raised.StopsAt);
        Assert.Equal(EAutoStopReason.Idle, raised.StopReason);
        Assert.Equal(30, raised.StopLimit);
        Assert.Equal("Integration server", raised.ServerTitle);
    }

    /// <summary>
    /// A cost owner without a setting row gets both channels skipped as disabled while the in-app alert is still
    /// created.
    /// </summary>
    /// <returns>A task completing after the added rows were verified.</returns>
    [Fact]
    public async Task AddWarning_OwnerWithoutSetting_SkipsBothChannelsButCreatesAlert()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.User.EMail = "operator@example.org";
        await store.DbContext.SaveChangesAsync();
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.MaxRuntime, 6);

        await CreateWriter(store).AddWarningAsync(
            pending,
            seed.Deployment!,
            "Integration server",
            Now,
            CancellationToken.None);
        await store.DbContext.SaveChangesAsync();

        Assert.Single(await store.DbContext.CostAlerts.AsNoTracking().ToListAsync());
        var deliveries = await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, q =>
        {
            Assert.Equal(ECostAlertDeliveryStatus.Skipped, q.Status);
            Assert.Equal(CostAlertDeliveryReasons.ChannelDisabled, q.StatusReason);
        });
    }

    /// <summary>
    /// When nobody resolves as a recipient, each channel gets one skipped no-recipient row and the alert is created.
    /// </summary>
    /// <returns>A task completing after the added rows were verified.</returns>
    [Fact]
    public async Task AddWarning_NobodyResolved_AddsNoRecipientRows()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedReachableOwnerAsync(store, discordEnabled: true, emailEnabled: true);
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.Schedule, null);
        var writer = new AutoStopAlertWriter(store.DbContext, new EmptyRecipientResolver());

        await writer.AddWarningAsync(pending, seed.Deployment!, "Integration server", Now, CancellationToken.None);
        await store.DbContext.SaveChangesAsync();

        Assert.Single(await store.DbContext.CostAlerts.AsNoTracking().ToListAsync());
        var deliveries = await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, q =>
        {
            Assert.Null(q.RecipientUserFk);
            Assert.Equal(ECostAlertDeliveryStatus.Skipped, q.Status);
            Assert.Equal(CostAlertDeliveryReasons.NoRecipient, q.StatusReason);
        });
    }

    /// <summary>
    /// The alert list of the cost owner's user shows the AutoStop alert with its stop facts and threshold alerts with
    /// the threshold kind and no stop facts.
    /// </summary>
    /// <returns>A task completing after the listed alerts were verified.</returns>
    [Fact]
    public async Task List_ShowsAutoStopAlertWithStopFactsAndThresholdAlertWithout()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedReachableOwnerAsync(store, discordEnabled: true, emailEnabled: true);
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.Idle, 30);
        var raised = await CreateWriter(store).AddWarningAsync(
            pending,
            seed.Deployment!,
            "Integration server",
            Now,
            CancellationToken.None);
        await store.DbContext.SaveChangesAsync();
        var threshold = new CostAlert
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment!.PrimaryKey,
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            ThresholdAmount = 5m,
            AccruedAmount = 5m,
            Currency = "EUR",
            TimeStampCreated = Now - Duration.FromMinutes(1),
        };
        store.DbContext.CostAlerts.Add(threshold);
        await store.DbContext.SaveChangesAsync();

        var listed = await CreateService(store).ListAsync(HostingTestData.Principal(seed.User), CancellationToken.None);

        Assert.Equal([raised.CostAlertId, threshold.PrimaryKey], listed.Select(q => q.Id).ToArray());
        var autoStop = listed.First();
        Assert.Equal(ECostAlertKind.AutoStop, autoStop.Kind);
        Assert.Equal(StopsAt, autoStop.StopsAt);
        Assert.Equal(EAutoStopReason.Idle, autoStop.StopReason);
        Assert.Equal(30, autoStop.StopLimit);
        var plain = listed.Last();
        Assert.Equal(ECostAlertKind.CostThreshold, plain.Kind);
        Assert.Null(plain.StopsAt);
        Assert.Null(plain.StopReason);
        Assert.Null(plain.StopLimit);
    }

    /// <summary>
    /// Resolving a pending stop skips its pending deliveries as stop-resolved, leaves sent and sending rows alone and
    /// returns the alert, server and cost owner ids.
    /// </summary>
    /// <returns>A task completing after the rows were verified.</returns>
    [Fact]
    public async Task Resolve_SkipsPendingDeliveriesOnly_AndReturnsAlertIds()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedReachableOwnerAsync(store, discordEnabled: true, emailEnabled: true);
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.Idle, 30);
        var writer = CreateWriter(store);
        var raised = await writer.AddWarningAsync(pending, seed.Deployment!, "x", Now, CancellationToken.None);
        await store.DbContext.SaveChangesAsync();
        var deliveries = await store.DbContext.CostAlertDeliveries.ToListAsync();
        var sending = deliveries.Single(q => q.Channel == ECostAlertChannel.Discord);
        sending.Status = ECostAlertDeliveryStatus.Sending;
        sending.TimeStampNextAttempt = null;
        var email = deliveries.Single(q => q.Channel == ECostAlertChannel.Email);
        var other = await AddRecipientUserAsync(store);
        var sent = new CostAlertDelivery
        {
            PrimaryKey = Guid.NewGuid(),
            CostAlertFk = raised.CostAlertId,
            Channel = ECostAlertChannel.Email,
            RecipientUserFk = other.PrimaryKey,
            Status = ECostAlertDeliveryStatus.Sent,
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        };
        store.DbContext.CostAlertDeliveries.Add(sent);
        await store.DbContext.SaveChangesAsync();
        var later = Now + Duration.FromMinutes(2);

        var resolved = await writer.ResolveAsync(pending.PrimaryKey, later, CancellationToken.None);
        await store.DbContext.SaveChangesAsync();

        Assert.NotNull(resolved);
        Assert.Equal(raised.CostAlertId, resolved.CostAlertId);
        Assert.Equal(seed.Server.PrimaryKey, resolved.GameServerId);
        Assert.Equal(seed.CostOwner.PrimaryKey, resolved.CostOwnerId);
        var stored = (await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync())
            .ToDictionary(q => q.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, stored[email.PrimaryKey].Status);
        Assert.Equal(CostAlertDeliveryReasons.StopResolved, stored[email.PrimaryKey].StatusReason);
        Assert.Null(stored[email.PrimaryKey].TimeStampNextAttempt);
        Assert.Equal(later, stored[email.PrimaryKey].TimeStampUpdated);
        Assert.Equal(ECostAlertDeliveryStatus.Sent, stored[sent.PrimaryKey].Status);
        Assert.Equal(ECostAlertDeliveryStatus.Sending, stored[sending.PrimaryKey].Status);
    }

    /// <summary>
    /// Resolving a pending stop without an alert returns null and changes nothing.
    /// </summary>
    /// <returns>A task completing after the result was verified.</returns>
    [Fact]
    public async Task Resolve_WithoutAlert_ReturnsNull()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedReachableOwnerAsync(store, discordEnabled: true, emailEnabled: true);
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.Idle, 30);

        var resolved = await CreateWriter(store).ResolveAsync(pending.PrimaryKey, Now, CancellationToken.None);

        Assert.Null(resolved);
    }

    /// <summary>
    /// An AutoStop row never counts as a recorded threshold crossing, even when its amount equals the threshold.
    /// </summary>
    /// <returns>A task completing after the evaluation result was verified.</returns>
    [Fact]
    public async Task Evaluator_IgnoresAutoStopRowsAsRecordedCrossings()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedReachableOwnerAsync(store, discordEnabled: true, emailEnabled: true, 1m);
        var sliceEnd = Now + Duration.FromMinutes(15);
        await HostingTestData.AddCostSliceAsync(store.DbContext, seed.Deployment!, Now, sliceEnd, 3.0m);
        var pending = await AddPendingStopAsync(store, seed, EAutoStopReason.Idle, 30);
        store.DbContext.CostAlerts.Add(new CostAlert
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment!.PrimaryKey,
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            Kind = ECostAlertKind.AutoStop,
            PendingStopFk = pending.PrimaryKey,
            ThresholdAmount = 1m,
            AccruedAmount = 0m,
            Currency = "EUR",
            TimeStampCreated = Now,
        });
        await store.DbContext.SaveChangesAsync();

        var result = await CreateEvaluator(store.DbContext).EvaluateAsync(sliceEnd, CancellationToken.None);

        var raised = Assert.Single(result.RaisedAlerts);
        Assert.Equal(ECostAlertKind.CostThreshold, raised.Kind);
        Assert.Equal(1m, raised.ThresholdAmount);
    }

    /// <summary>
    /// Adds and saves a second user that only serves as the recipient of a delivery row.
    /// </summary>
    /// <param name="store">The store the user is added to.</param>
    /// <returns>A task resolving to the saved user.</returns>
    private static async Task<User> AddRecipientUserAsync(HostingTestStore store)
    {
        var user = new User
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = "second-recipient",
            AvatarMimeType = string.Empty,
            Claims = [],
            Roles = [],
            Discord = new DiscordUser { Username = "second-recipient" },
            Steam = new SteamUser(),
        };
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Creates the writer under test with the real recipient resolver.
    /// </summary>
    /// <param name="store">The store whose context the writer uses.</param>
    /// <returns>The writer under test.</returns>
    private static AutoStopAlertWriter CreateWriter(HostingTestStore store)
        => new(store.DbContext, new CostAlertRecipientResolver(store.DbContext));

    /// <summary>
    /// Creates the alert service used to list alerts.
    /// </summary>
    /// <param name="store">The store whose context the service uses.</param>
    /// <returns>The service under test.</returns>
    private static CostAlertService CreateService(HostingTestStore store)
        => new(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            new IgnoringPublisher(),
            NullLogger<CostAlertService>.Instance);

    /// <summary>
    /// Creates the threshold evaluator with real accrual and recipient services.
    /// </summary>
    /// <param name="dbContext">The database context the evaluator reads and writes.</param>
    /// <returns>The evaluator under test.</returns>
    private static CostAlertEvaluator CreateEvaluator(DatabaseContext dbContext)
        => new(
            dbContext,
            new CostAccrualService(
                dbContext,
                new HostingAuthorizationService(dbContext),
                [new TestRuntimeProvider(), new EmulatedHetznerRuntimeProvider()]),
            new CostAlertRecipientResolver(dbContext),
            Options.Create(new CostAlertOptions()),
            NullLogger<CostAlertEvaluator>.Instance);

    /// <summary>
    /// Seeds a server with an active deployment whose owner user can be reached by Discord and email, and stores the
    /// owner's channel flags.
    /// </summary>
    /// <param name="store">The store to seed.</param>
    /// <param name="discordEnabled">Whether the owner's setting enables Discord.</param>
    /// <param name="emailEnabled">Whether the owner's setting enables email.</param>
    /// <param name="amounts">The threshold amounts of the owner.</param>
    /// <returns>A task resolving to the seeded entities.</returns>
    private static async Task<HostingTestSeed> SeedReachableOwnerAsync(
        HostingTestStore store,
        bool discordEnabled,
        bool emailEnabled,
        params decimal[] amounts)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.User.Discord.Id = 4711;
        seed.User.EMail = "operator@example.org";
        await store.DbContext.SaveChangesAsync();
        await HostingTestData.SetCostAlertThresholdsAsync(
            store.DbContext, seed.CostOwner.PrimaryKey, discordEnabled, emailEnabled, amounts);
        return seed;
    }

    /// <summary>
    /// Adds and saves a pending stop for the seeded deployment.
    /// </summary>
    /// <param name="store">The store the pending stop is added to.</param>
    /// <param name="seed">The seeded server and deployment.</param>
    /// <param name="reason">The reason of the stop.</param>
    /// <param name="limit">The limit of the stop, or <see langword="null"/> for a schedule stop.</param>
    /// <returns>A task resolving to the saved pending stop.</returns>
    internal static async Task<AutomationPendingStop> AddPendingStopAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        EAutoStopReason reason,
        int? limit)
    {
        var pending = new AutomationPendingStop
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            Reason = reason,
            Limit = limit,
            WarnedAt = Now,
            StopsAt = StopsAt,
            Status = EAutoStopStatus.Pending,
            TimeStampCreated = Now,
        };
        store.DbContext.Set<AutomationPendingStop>().Add(pending);
        await store.DbContext.SaveChangesAsync();
        return pending;
    }

    /// <summary>
    /// A recipient resolver that resolves nobody for every cost owner.
    /// </summary>
    private sealed class EmptyRecipientResolver : ICostAlertRecipientResolver
    {
        /// <inheritdoc />
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<CostAlertRecipient>>> ResolveAsync(
            IReadOnlyCollection<Guid> costOwnerIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<CostAlertRecipient>>>(
                costOwnerIds.ToDictionary(q => q, _ => (IReadOnlyList<CostAlertRecipient>) []));
    }

    /// <summary>
    /// A publisher double that ignores every publication.
    /// </summary>
    private sealed class IgnoringPublisher : ICostAlertPublisher
    {
        /// <inheritdoc />
        public Task PublishAccrualsAsync(IReadOnlyList<DeploymentAccrual> accruals, CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishRaisedAsync(IReadOnlyList<RaisedCostAlert> alerts, CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishDismissedAsync(
            Guid userId,
            IReadOnlyCollection<Guid> costAlertIds,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishResolvedAsync(IReadOnlyList<ResolvedCostAlert> alerts, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
