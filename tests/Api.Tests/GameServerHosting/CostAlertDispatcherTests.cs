using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Database.Hosting;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-05 proof that the dispatcher claims due outbox rows before sending, delivers each alert once per channel and
/// recipient, and never claims a terminal row again (D-14). The tests run the real
/// <see cref="CostAlertDeliveryProcessor" /> against in-memory SQLite with recording channel fakes.
/// </summary>
public sealed class CostAlertDispatcherTests
{
    /// <summary>
    /// The instant every seeded alert and delivery is created at.
    /// </summary>
    private static readonly Instant Start = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// A pending Discord row for a German recipient is claimed, delivered as one direct message and recorded as sent
    /// with its attempt count and without a lease; a later pass never sends it again.
    /// </summary>
    /// <returns>A task completing after the persisted row and the recorded messages were verified.</returns>
    [Fact]
    public async Task PendingDiscordRow_IsSentOnceInRecipientLanguage_AndNeverClaimedAgain()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, locale: "de", discordId: 42);
        var discord = new RecordingDiscordDirectMessageSender();
        var processor = CreateProcessor(store, discord, new RecordingEmailSender());

        var claimed = await processor.ProcessDueAsync(Start + Duration.FromSeconds(1), CancellationToken.None);

        Assert.Equal(1, claimed);
        var message = Assert.Single(discord.Sent);
        Assert.Equal(42UL, message.DiscordUserId);
        Assert.Contains("Kostenwarnung", message.Text);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Sent, row.Status);
        Assert.Equal(Start + Duration.FromSeconds(1), row.TimeStampSent);
        Assert.Equal(1, row.AttemptCount);
        Assert.Null(row.LeaseOwner);
        Assert.Null(row.LeaseExpires);
        Assert.Null(row.StatusReason);

        var second = await processor.ProcessDueAsync(Start + Duration.FromHours(5), CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Single(discord.Sent);
    }

    /// <summary>
    /// A pending row whose next attempt lies in the future is not claimed and nothing is sent.
    /// </summary>
    /// <returns>A task completing after the untouched row was verified.</returns>
    [Fact]
    public async Task PendingRow_WithFutureNextAttempt_IsNotClaimed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(
            store,
            ECostAlertChannel.Discord,
            discordId: 42,
            nextAttempt: Start + Duration.FromMinutes(10));
        var discord = new RecordingDiscordDirectMessageSender();

        var claimed = await CreateProcessor(store, discord, new RecordingEmailSender())
            .ProcessDueAsync(Start + Duration.FromMinutes(9), CancellationToken.None);

        Assert.Equal(0, claimed);
        Assert.Empty(discord.Sent);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Pending, row.Status);
        Assert.Equal(0, row.AttemptCount);
    }

    /// <summary>
    /// Sent, failed and skipped rows are terminal: no pass claims them, whatever the instant.
    /// </summary>
    /// <param name="status">The terminal status the row is seeded with.</param>
    /// <returns>A task completing after the untouched row was verified.</returns>
    [Theory]
    [InlineData(ECostAlertDeliveryStatus.Sent)]
    [InlineData(ECostAlertDeliveryStatus.Failed)]
    [InlineData(ECostAlertDeliveryStatus.Skipped)]
    public async Task TerminalRows_AreNeverClaimed(ECostAlertDeliveryStatus status)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42, status: status);
        var discord = new RecordingDiscordDirectMessageSender();

        var claimed = await CreateProcessor(store, discord, new RecordingEmailSender())
            .ProcessDueAsync(Start + Duration.FromDays(30), CancellationToken.None);

        Assert.Equal(0, claimed);
        Assert.Empty(discord.Sent);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(status, row.Status);
        Assert.Equal(0, row.AttemptCount);
    }

    /// <summary>
    /// A transient result keeps the row pending with the next delay of the backoff list (30 s, 2 min, 10 min, 1 h),
    /// nothing is attempted before the delay elapsed, and the fifth transient result fails the row with no sixth
    /// attempt.
    /// </summary>
    /// <returns>A task completing after the whole backoff sequence was verified.</returns>
    [Fact]
    public async Task TransientResults_RetryWithBackoff_AndFailAfterFifthAttempt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42);
        var discord = new RecordingDiscordDirectMessageSender();
        discord.Script(
            Enumerable.Repeat(DeliveryAttemptResult.Transient("discord-not-connected"), 5).ToArray());
        var processor = CreateProcessor(store, discord, new RecordingEmailSender());
        TimeSpan[] expectedDelays =
        [
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromHours(1),
        ];

        var now = Start;
        for (var attempt = 1; attempt <= expectedDelays.Length; attempt++)
        {
            Assert.Equal(1, await processor.ProcessDueAsync(now, CancellationToken.None));
            var row = await ReloadAsync(store, delivery.PrimaryKey);
            Assert.Equal(ECostAlertDeliveryStatus.Pending, row.Status);
            Assert.Equal(attempt, row.AttemptCount);
            Assert.Equal("discord-not-connected", row.StatusReason);
            Assert.Null(row.LeaseOwner);
            var expectedNext = now + Duration.FromTimeSpan(expectedDelays[attempt - 1]);
            Assert.Equal(expectedNext, row.TimeStampNextAttempt);

            Assert.Equal(0, await processor.ProcessDueAsync(
                expectedNext - Duration.FromSeconds(1),
                CancellationToken.None));
            Assert.Equal(attempt, discord.Sent.Count);
            now = expectedNext + Duration.FromSeconds(1);
        }

        Assert.Equal(1, await processor.ProcessDueAsync(now, CancellationToken.None));
        var failed = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Failed, failed.Status);
        Assert.Equal("retries-exhausted", failed.StatusReason);
        Assert.Equal(5, failed.AttemptCount);
        Assert.Null(failed.TimeStampNextAttempt);
        Assert.Null(failed.LeaseOwner);

        Assert.Equal(0, await processor.ProcessDueAsync(now + Duration.FromDays(30), CancellationToken.None));
        Assert.Equal(5, discord.Sent.Count);
    }

    /// <summary>
    /// A permanent result fails the row after one attempt and it is never retried.
    /// </summary>
    /// <returns>A task completing after the failed row was verified.</returns>
    [Fact]
    public async Task PermanentResult_FailsAfterOneAttempt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42);
        var discord = new RecordingDiscordDirectMessageSender();
        discord.Script(DeliveryAttemptResult.Permanent("discord-dm-blocked"));
        var processor = CreateProcessor(store, discord, new RecordingEmailSender());

        Assert.Equal(1, await processor.ProcessDueAsync(Start, CancellationToken.None));
        Assert.Equal(0, await processor.ProcessDueAsync(Start + Duration.FromDays(1), CancellationToken.None));

        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Failed, row.Status);
        Assert.Equal("discord-dm-blocked", row.StatusReason);
        Assert.Equal(1, row.AttemptCount);
        Assert.Single(discord.Sent);
    }

    /// <summary>
    /// A channel that reports itself as not configured skips the row with its reason and never retries it.
    /// </summary>
    /// <returns>A task completing after the skipped row was verified.</returns>
    [Fact]
    public async Task NotConfiguredResult_SkipsRowWithItsReason()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42);
        var discord = new RecordingDiscordDirectMessageSender();
        discord.Script(DeliveryAttemptResult.NotConfigured("not-configured"));
        var processor = CreateProcessor(store, discord, new RecordingEmailSender());

        Assert.Equal(1, await processor.ProcessDueAsync(Start, CancellationToken.None));
        Assert.Equal(0, await processor.ProcessDueAsync(Start + Duration.FromDays(1), CancellationToken.None));

        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, row.Status);
        Assert.Equal("not-configured", row.StatusReason);
        Assert.Single(discord.Sent);
    }

    /// <summary>
    /// A recipient without a linked Discord account is skipped as not linked and no message is sent.
    /// </summary>
    /// <returns>A task completing after the skipped row was verified.</returns>
    [Fact]
    public async Task UnlinkedDiscordRecipient_IsSkippedAsNotLinked()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 0);
        var discord = new RecordingDiscordDirectMessageSender();

        await CreateProcessor(store, discord, new RecordingEmailSender())
            .ProcessDueAsync(Start, CancellationToken.None);

        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, row.Status);
        Assert.Equal("not-linked", row.StatusReason);
        Assert.Empty(discord.Sent);
    }

    /// <summary>
    /// A banned, deleted or missing recipient is skipped as inactive on every channel and nothing is sent.
    /// </summary>
    /// <param name="channel">The channel of the delivery.</param>
    /// <param name="banned">Whether the recipient is banned.</param>
    /// <param name="deleted">Whether the recipient is deleted.</param>
    /// <param name="withoutRecipient">Whether the delivery has no recipient at all.</param>
    /// <returns>A task completing after the skipped row was verified.</returns>
    [Theory]
    [InlineData(ECostAlertChannel.Discord, true, false, false)]
    [InlineData(ECostAlertChannel.Email, false, true, false)]
    [InlineData(ECostAlertChannel.Discord, false, false, true)]
    public async Task InactiveRecipient_IsSkippedAsRecipientInactive(
        ECostAlertChannel channel,
        bool banned,
        bool deleted,
        bool withoutRecipient)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(
            store,
            channel,
            discordId: 42,
            email: "operator@example.org",
            banned: banned,
            deleted: deleted,
            withoutRecipient: withoutRecipient);
        var discord = new RecordingDiscordDirectMessageSender();
        var email = new RecordingEmailSender();

        await CreateProcessor(store, discord, email).ProcessDueAsync(Start, CancellationToken.None);

        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, row.Status);
        Assert.Equal("recipient-inactive", row.StatusReason);
        Assert.Empty(discord.Sent);
        Assert.Empty(email.Sent);
    }

    /// <summary>
    /// A sending row whose lease expired under another owner is reclaimed, sent and marked sent with the attempt
    /// count incremented.
    /// </summary>
    /// <returns>A task completing after the recovered row was verified.</returns>
    [Fact]
    public async Task SendingRow_WithExpiredForeignLease_IsReclaimedAndSent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(
            store,
            ECostAlertChannel.Discord,
            discordId: 42,
            status: ECostAlertDeliveryStatus.Sending,
            attemptCount: 1,
            leaseOwner: "crashed-process",
            leaseExpires: Start + Duration.FromMinutes(2));
        var discord = new RecordingDiscordDirectMessageSender();

        var claimed = await CreateProcessor(store, discord, new RecordingEmailSender())
            .ProcessDueAsync(Start + Duration.FromMinutes(3), CancellationToken.None);

        Assert.Equal(1, claimed);
        Assert.Single(discord.Sent);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Sent, row.Status);
        Assert.Equal(2, row.AttemptCount);
        Assert.Null(row.LeaseOwner);
    }

    /// <summary>
    /// A sending row whose lease has not expired belongs to another owner and is left untouched.
    /// </summary>
    /// <returns>A task completing after the untouched row was verified.</returns>
    [Fact]
    public async Task SendingRow_WithUnexpiredLease_IsNotTouched()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(
            store,
            ECostAlertChannel.Discord,
            discordId: 42,
            status: ECostAlertDeliveryStatus.Sending,
            attemptCount: 1,
            leaseOwner: "other-process",
            leaseExpires: Start + Duration.FromMinutes(2));
        var discord = new RecordingDiscordDirectMessageSender();

        var claimed = await CreateProcessor(store, discord, new RecordingEmailSender())
            .ProcessDueAsync(Start + Duration.FromMinutes(1), CancellationToken.None);

        Assert.Equal(0, claimed);
        Assert.Empty(discord.Sent);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Sending, row.Status);
        Assert.Equal("other-process", row.LeaseOwner);
        Assert.Equal(1, row.AttemptCount);
    }

    /// <summary>
    /// An outcome write from a process that lost its lease to another owner changes nothing.
    /// </summary>
    /// <returns>A task completing after the foreign lease was verified to survive.</returns>
    [Fact]
    public async Task OutcomeWrite_WithoutTheLease_ChangesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42);
        var stealing = new LeaseStealingSender(store, delivery.PrimaryKey);

        var claimed = await new CostAlertDeliveryProcessor(
                store.DbContext,
                stealing,
                new RecordingEmailSender(),
                new BaseUrl(string.Empty, string.Empty),
                Options.Create(new CostAlertOptions()),
                NullLogger<CostAlertDeliveryProcessor>.Instance)
            .ProcessDueAsync(Start, CancellationToken.None);

        Assert.Equal(1, claimed);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Sending, row.Status);
        Assert.Equal("new-owner", row.LeaseOwner);
        Assert.Null(row.TimeStampSent);
    }

    /// <summary>
    /// An unexpected exception while sending becomes a transient failure with a fixed reason code and never stores
    /// the exception text.
    /// </summary>
    /// <returns>A task completing after the pending row was verified.</returns>
    [Fact]
    public async Task UnexpectedSendException_IsTransientWithFixedReason()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42);
        var discord = new RecordingDiscordDirectMessageSender
        {
            ThrowOnSend = new InvalidOperationException("secret-token-123"),
        };

        await CreateProcessor(store, discord, new RecordingEmailSender())
            .ProcessDueAsync(Start, CancellationToken.None);

        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Pending, row.Status);
        Assert.Equal("dispatch-error", row.StatusReason);
        Assert.Equal(Start + Duration.FromSeconds(30), row.TimeStampNextAttempt);
    }

    /// <summary>
    /// A cancellation during the send propagates and leaves the row leased, so it is reclaimed after the lease
    /// expired.
    /// </summary>
    /// <returns>A task completing after the leased row and its recovery were verified.</returns>
    [Fact]
    public async Task CancellationDuringSend_PropagatesAndLeavesRowLeased()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var delivery = await SeedDeliveryAsync(store, ECostAlertChannel.Discord, discordId: 42);
        using var cancellation = new CancellationTokenSource();
        var cancelling = new CancellingSender(cancellation);
        var processor = new CostAlertDeliveryProcessor(
            store.DbContext,
            cancelling,
            new RecordingEmailSender(),
            new BaseUrl(string.Empty, string.Empty),
            Options.Create(new CostAlertOptions()),
            NullLogger<CostAlertDeliveryProcessor>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessDueAsync(Start, cancellation.Token));

        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Sending, row.Status);
        Assert.NotNull(row.LeaseOwner);
        Assert.Equal(Start + Duration.FromMinutes(2), row.LeaseExpires);

        var recovery = new RecordingDiscordDirectMessageSender();
        var claimed = await CreateProcessor(store, recovery, new RecordingEmailSender())
            .ProcessDueAsync(Start + Duration.FromMinutes(3), CancellationToken.None);
        Assert.Equal(1, claimed);
        Assert.Single(recovery.Sent);
        Assert.Equal(ECostAlertDeliveryStatus.Sent, (await ReloadAsync(store, delivery.PrimaryKey)).Status);
    }

    /// <summary>
    /// A Discord sender that takes the lease over while the message is "in flight", standing in for another
    /// dispatcher that reclaimed the row after this process stalled.
    /// </summary>
    /// <param name="store">The store whose delivery row is taken over.</param>
    /// <param name="deliveryId">The delivery whose lease is taken over.</param>
    private sealed class LeaseStealingSender(HostingTestStore store, Guid deliveryId) : IDiscordDirectMessageSender
    {
        /// <inheritdoc />
        public async Task<DeliveryAttemptResult> SendAsync(
            ulong discordUserId,
            string text,
            CancellationToken cancellationToken)
        {
            await store.DbContext.CostAlertDeliveries
                .Where(q => q.PrimaryKey == deliveryId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(q => q.LeaseOwner, "new-owner"),
                    cancellationToken);
            return DeliveryAttemptResult.Sent();
        }
    }

    /// <summary>
    /// A Discord sender that cancels the pass while the message is being sent.
    /// </summary>
    /// <param name="cancellation">The source that is cancelled during the send.</param>
    private sealed class CancellingSender(CancellationTokenSource cancellation) : IDiscordDirectMessageSender
    {
        /// <inheritdoc />
        public Task<DeliveryAttemptResult> SendAsync(
            ulong discordUserId,
            string text,
            CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DeliveryAttemptResult.Sent());
        }
    }

    /// <summary>
    /// Creates the processor under test with the given channel fakes and an unconfigured client URL.
    /// </summary>
    /// <param name="store">The test store whose context the processor uses.</param>
    /// <param name="discord">The Discord fake.</param>
    /// <param name="email">The email fake.</param>
    /// <returns>The processor.</returns>
    private static CostAlertDeliveryProcessor CreateProcessor(
        HostingTestStore store,
        RecordingDiscordDirectMessageSender discord,
        RecordingEmailSender email)
        => new(
            store.DbContext,
            discord,
            email,
            new BaseUrl(string.Empty, string.Empty),
            Options.Create(new CostAlertOptions()),
            NullLogger<CostAlertDeliveryProcessor>.Instance);

    /// <summary>
    /// Reads a delivery row from the database without tracking, so the result is never a stale tracked instance.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="deliveryId">The delivery to read.</param>
    /// <returns>A task resolving to the persisted row.</returns>
    private static Task<CostAlertDelivery> ReloadAsync(HostingTestStore store, Guid deliveryId)
        => store.DbContext.CostAlertDeliveries.AsNoTracking().SingleAsync(q => q.PrimaryKey == deliveryId);

    /// <summary>
    /// Seeds a server with a deployment, one alert for it and one delivery row of the given channel for the owner
    /// user.
    /// </summary>
    /// <param name="store">The test store to seed.</param>
    /// <param name="channel">The channel of the delivery.</param>
    /// <param name="locale">The recipient's recorded locale.</param>
    /// <param name="discordId">The recipient's Discord id; zero means not linked.</param>
    /// <param name="email">The recipient's email address.</param>
    /// <param name="status">The seeded delivery status.</param>
    /// <param name="nextAttempt">The earliest instant of the next attempt, or <see langword="null" />.</param>
    /// <param name="attemptCount">The seeded number of attempts made so far.</param>
    /// <param name="leaseOwner">The seeded lease owner, or <see langword="null" />.</param>
    /// <param name="leaseExpires">The seeded lease expiry, or <see langword="null" />.</param>
    /// <param name="banned">Whether the recipient is banned.</param>
    /// <param name="deleted">Whether the recipient is deleted.</param>
    /// <param name="withoutRecipient">Whether the delivery row has no recipient.</param>
    /// <returns>A task resolving to the persisted delivery row.</returns>
    private static async Task<CostAlertDelivery> SeedDeliveryAsync(
        HostingTestStore store,
        ECostAlertChannel channel,
        string? locale = null,
        ulong discordId = 0,
        string? email = null,
        ECostAlertDeliveryStatus status = ECostAlertDeliveryStatus.Pending,
        Instant? nextAttempt = null,
        int attemptCount = 0,
        string? leaseOwner = null,
        Instant? leaseExpires = null,
        bool banned = false,
        bool deleted = false,
        bool withoutRecipient = false)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.User.Locale = locale;
        seed.User.Discord.Id = discordId;
        seed.User.EMail = email;
        seed.User.IsBanned = banned;
        seed.User.IsDeleted = deleted;
        var alert = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = seed.Deployment!.PrimaryKey,
            GameServerFk     = seed.Server.PrimaryKey,
            CostOwnerFk      = seed.CostOwner.PrimaryKey,
            ThresholdAmount  = 5m,
            AccruedAmount    = 5.25m,
            Currency         = "EUR",
            TimeStampCreated = Start,
        };
        var delivery = new CostAlertDelivery
        {
            PrimaryKey           = Guid.NewGuid(),
            CostAlertFk          = alert.PrimaryKey,
            Channel              = channel,
            RecipientUserFk      = withoutRecipient ? null : seed.User.PrimaryKey,
            Status               = status,
            AttemptCount         = attemptCount,
            LeaseOwner           = leaseOwner,
            LeaseExpires         = leaseExpires,
            TimeStampNextAttempt = nextAttempt ?? Start,
            TimeStampCreated     = Start,
            TimeStampUpdated     = Start,
        };
        store.DbContext.CostAlerts.Add(alert);
        store.DbContext.CostAlertDeliveries.Add(delivery);
        await store.DbContext.SaveChangesAsync();
        return delivery;
    }
}
