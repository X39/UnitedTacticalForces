using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-06 proof that email deliveries are skipped cleanly while email is unconfigured, send exactly one email per
/// recipient and alert in the recipient's language with a deterministic Message-Id, and never write an address,
/// subject or body to the logs (D-11, D-12). The tests run the real <see cref="CostAlertDeliveryProcessor" />
/// against in-memory SQLite with a recording email sender.
/// </summary>
public sealed class CostAlertEmailDeliveryTests
{
    /// <summary>
    /// The instant every seeded alert and delivery is created at.
    /// </summary>
    private static readonly Instant Start = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// The address of the first recipient, which must never appear in a log.
    /// </summary>
    private const string FirstAddress = "first.recipient@example.org";

    /// <summary>
    /// The address of the second recipient, which must never appear in a log.
    /// </summary>
    private const string SecondAddress = "second.recipient@example.org";

    /// <summary>
    /// While email is not configured the row is skipped as not configured and no email is sent.
    /// </summary>
    /// <returns>A task completing after the skipped row was verified.</returns>
    [Fact]
    public async Task UnconfiguredEmail_SkipsRowAndSendsNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAlertAsync(store);
        var delivery = await AddEmailDeliveryAsync(store, seed.Alert, seed.First);
        var email = new RecordingEmailSender { IsConfigured = false };

        var claimed = await CreateProcessor(store, email).ProcessDueAsync(Start, CancellationToken.None);

        Assert.Equal(1, claimed);
        Assert.Empty(email.Sent);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, row.Status);
        Assert.Equal("not-configured", row.StatusReason);
    }

    /// <summary>
    /// A group alert with two recipients sends exactly one email to each address, with its own deterministic
    /// Message-Id, in German for the recipient with locale "de" and in English for the one without a locale; a
    /// second pass sends nothing more.
    /// </summary>
    /// <returns>A task completing after both emails were verified.</returns>
    [Fact]
    public async Task GroupAlert_SendsOneLocalizedEmailPerRecipient()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAlertAsync(store);
        seed.First.Locale = "de";
        await store.DbContext.SaveChangesAsync();
        await AddEmailDeliveryAsync(store, seed.Alert, seed.First);
        await AddEmailDeliveryAsync(store, seed.Alert, seed.Second);
        var email = new RecordingEmailSender();
        var processor = CreateProcessor(store, email);

        Assert.Equal(2, await processor.ProcessDueAsync(Start, CancellationToken.None));
        Assert.Equal(0, await processor.ProcessDueAsync(Start + Duration.FromHours(1), CancellationToken.None));

        Assert.Equal(2, email.Sent.Count);
        var german = Assert.Single(email.Sent, q => q.ToAddress == FirstAddress);
        var english = Assert.Single(email.Sent, q => q.ToAddress == SecondAddress);
        Assert.Contains("Kostenwarnung", german.Subject);
        Assert.Contains("Cost alert", english.Subject);
        Assert.Equal(email.CreateMessageId(seed.Alert.PrimaryKey, seed.First.PrimaryKey), german.MessageId);
        Assert.Equal(email.CreateMessageId(seed.Alert.PrimaryKey, seed.Second.PrimaryKey), english.MessageId);
        Assert.NotEqual(german.MessageId, english.MessageId);
        Assert.All(
            await store.DbContext.CostAlertDeliveries.AsNoTracking().ToListAsync(),
            q => Assert.Equal(ECostAlertDeliveryStatus.Sent, q.Status));
    }

    /// <summary>
    /// An automatic stop alert is rendered with the automatic shutdown text in the recipient's language and sent
    /// with the deterministic Message-Id, while a threshold alert of the same pass keeps the threshold text.
    /// </summary>
    /// <returns>A task completing after both emails were verified.</returns>
    [Fact]
    public async Task AutoStopAlert_IsRenderedAsWarningWithDeterministicMessageId()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAlertAsync(store);
        seed.First.Locale = "de";
        await store.DbContext.SaveChangesAsync();
        var pending = new AutomationPendingStop
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = seed.Alert.GameServerFk,
            DeploymentFk     = seed.Alert.DeploymentFk,
            Reason           = EAutoStopReason.Idle,
            Limit            = 30,
            WarnedAt         = Start,
            StopsAt          = Start + Duration.FromMinutes(5),
            Status           = EAutoStopStatus.Pending,
            TimeStampCreated = Start,
        };
        var autoStop = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = seed.Alert.DeploymentFk,
            GameServerFk     = seed.Alert.GameServerFk,
            CostOwnerFk      = seed.Alert.CostOwnerFk,
            Kind             = ECostAlertKind.AutoStop,
            PendingStopFk    = pending.PrimaryKey,
            Currency         = "EUR",
            TimeStampCreated = Start,
        };
        store.DbContext.Set<AutomationPendingStop>().Add(pending);
        store.DbContext.CostAlerts.Add(autoStop);
        await store.DbContext.SaveChangesAsync();
        await AddEmailDeliveryAsync(store, autoStop, seed.First);
        await AddEmailDeliveryAsync(store, seed.Alert, seed.Second);
        var email = new RecordingEmailSender();

        Assert.Equal(2, await CreateProcessor(store, email).ProcessDueAsync(Start, CancellationToken.None));

        var warning = Assert.Single(email.Sent, q => q.ToAddress == FirstAddress);
        Assert.StartsWith("Automatisches Herunterfahren von Integration server um ", warning.Subject);
        Assert.Contains("Seit 30 Minuten war niemand auf Integration server.", warning.Body);
        Assert.Contains(
            $"https://utf.test/next/command/servers/{seed.Server.PrimaryKey}?tab=settings#server-automation",
            warning.Body);
        Assert.Equal(email.CreateMessageId(autoStop.PrimaryKey, seed.First.PrimaryKey), warning.MessageId);
        var threshold = Assert.Single(email.Sent, q => q.ToAddress == SecondAddress);
        Assert.Contains("Cost alert", threshold.Subject);
    }

    /// <summary>
    /// A recipient whose email address was cleared after the crossing is skipped as having no address.
    /// </summary>
    /// <returns>A task completing after the skipped row was verified.</returns>
    [Fact]
    public async Task RecipientWithClearedAddress_IsSkippedAsNoAddress()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAlertAsync(store);
        var delivery = await AddEmailDeliveryAsync(store, seed.Alert, seed.First);
        seed.First.EMail = null;
        await store.DbContext.SaveChangesAsync();
        var email = new RecordingEmailSender();

        await CreateProcessor(store, email).ProcessDueAsync(Start, CancellationToken.None);

        Assert.Empty(email.Sent);
        var row = await ReloadAsync(store, delivery.PrimaryKey);
        Assert.Equal(ECostAlertDeliveryStatus.Skipped, row.Status);
        Assert.Equal("no-address", row.StatusReason);
    }

    /// <summary>
    /// With an absolute client base URL the body contains the link to the server; without one it contains the
    /// sentence without a link.
    /// </summary>
    /// <param name="clientUrl">The configured client base URL.</param>
    /// <param name="expectLink">Whether the body must contain the server link.</param>
    /// <returns>A task completing after the body was verified.</returns>
    [Theory]
    [InlineData("https://utf.test/", true)]
    [InlineData("", false)]
    public async Task Body_ContainsServerLinkOnlyForAbsoluteClientUrl(string clientUrl, bool expectLink)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAlertAsync(store);
        await AddEmailDeliveryAsync(store, seed.Alert, seed.First);
        var email = new RecordingEmailSender();

        await CreateProcessor(store, email, clientUrl: clientUrl).ProcessDueAsync(Start, CancellationToken.None);

        var body = Assert.Single(email.Sent).Body;
        var link = $"https://utf.test/next/command/servers/{seed.Server.PrimaryKey}";
        if (expectLink)
        {
            Assert.Contains(link, body);
            return;
        }

        Assert.DoesNotContain("https://", body);
        Assert.Contains("Open the server in the app to shut it down and stop billing.", body);
    }

    /// <summary>
    /// The captured log output of a sent email, a permanently failed email and an email whose send threw an
    /// exception that carries the address contains neither the address, the subject, the body nor the exception
    /// text.
    /// </summary>
    /// <returns>A task completing after the log output was verified.</returns>
    [Fact]
    public async Task Logs_NeverContainAddressSubjectBodyOrExceptionText()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedAlertAsync(store);
        await AddEmailDeliveryAsync(store, seed.Alert, seed.First);
        await AddEmailDeliveryAsync(store, seed.Alert, seed.Second);
        var email = new RecordingEmailSender();
        email.Script(DeliveryAttemptResult.Sent(), DeliveryAttemptResult.Permanent("smtp-rejected-550"));
        var logger = new CapturingLogger();
        var processor = CreateProcessor(store, email, logger);

        await processor.ProcessDueAsync(Start, CancellationToken.None);
        email.ThrowOnSend = new InvalidOperationException($"connection to {FirstAddress} failed: password=hunter2");
        var laterAlert = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = seed.Alert.DeploymentFk,
            GameServerFk     = seed.Alert.GameServerFk,
            CostOwnerFk      = seed.Alert.CostOwnerFk,
            ThresholdAmount  = 10m,
            AccruedAmount    = 10.5m,
            Currency         = "EUR",
            TimeStampCreated = Start,
        };
        store.DbContext.CostAlerts.Add(laterAlert);
        await store.DbContext.SaveChangesAsync();
        await AddEmailDeliveryAsync(store, laterAlert, seed.First);
        await processor.ProcessDueAsync(Start, CancellationToken.None);

        Assert.Equal(3, email.Sent.Count);
        var output = logger.Output;
        Assert.NotEmpty(output);
        foreach (var sent in email.Sent)
        {
            Assert.DoesNotContain(sent.ToAddress, output);
            Assert.DoesNotContain(sent.Subject, output);
            Assert.DoesNotContain(sent.Body, output);
        }

        Assert.DoesNotContain(FirstAddress, output);
        Assert.DoesNotContain(SecondAddress, output);
        Assert.DoesNotContain("Integration server", output);
        Assert.DoesNotContain("hunter2", output);
    }

    /// <summary>
    /// Creates the processor under test with the given email fake, a disconnected Discord fake and a client URL.
    /// </summary>
    /// <param name="store">The test store whose context the processor uses.</param>
    /// <param name="email">The email fake.</param>
    /// <param name="logger">The logger the processor writes to, or <see langword="null" /> for a discarding one.</param>
    /// <param name="clientUrl">The configured client base URL.</param>
    /// <returns>The processor.</returns>
    private static CostAlertDeliveryProcessor CreateProcessor(
        HostingTestStore store,
        RecordingEmailSender email,
        ILogger<CostAlertDeliveryProcessor>? logger = null,
        string clientUrl = "https://utf.test/")
        => new(
            store.DbContext,
            new RecordingDiscordDirectMessageSender(),
            email,
            new BaseUrl("https://utf.test/api/", clientUrl),
            Options.Create(new CostAlertOptions()),
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CostAlertDeliveryProcessor>.Instance);

    /// <summary>
    /// Reads a delivery row from the database without tracking, so the result is never a stale tracked instance.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="deliveryId">The delivery to read.</param>
    /// <returns>A task resolving to the persisted row.</returns>
    private static Task<CostAlertDelivery> ReloadAsync(HostingTestStore store, Guid deliveryId)
        => store.DbContext.CostAlertDeliveries.AsNoTracking().SingleAsync(q => q.PrimaryKey == deliveryId);

    /// <summary>
    /// Seeds a server with a deployment, one alert for it and two users with an email address each.
    /// </summary>
    /// <param name="store">The test store to seed.</param>
    /// <returns>A task resolving to the seeded entities.</returns>
    private static async Task<AlertSeed> SeedAlertAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.User.EMail = FirstAddress;
        var second = new User
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = "second-recipient-" + Guid.NewGuid().ToString("N"),
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord        = new DiscordUser { Username = "second-recipient" },
            Steam          = new SteamUser(),
            EMail          = SecondAddress,
        };
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
        store.DbContext.Users.Add(second);
        store.DbContext.CostAlerts.Add(alert);
        await store.DbContext.SaveChangesAsync();
        return new AlertSeed(alert, seed.Server, seed.User, second);
    }

    /// <summary>
    /// Adds a pending email delivery of the alert for the given recipient.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="alert">The alert that is delivered.</param>
    /// <param name="recipient">The recipient of the delivery.</param>
    /// <returns>A task resolving to the persisted delivery row.</returns>
    private static async Task<CostAlertDelivery> AddEmailDeliveryAsync(
        HostingTestStore store,
        CostAlert alert,
        User recipient)
    {
        var delivery = new CostAlertDelivery
        {
            PrimaryKey           = Guid.NewGuid(),
            CostAlertFk          = alert.PrimaryKey,
            Channel              = ECostAlertChannel.Email,
            RecipientUserFk      = recipient.PrimaryKey,
            Status               = ECostAlertDeliveryStatus.Pending,
            TimeStampNextAttempt = Start,
            TimeStampCreated     = Start,
            TimeStampUpdated     = Start,
        };
        store.DbContext.CostAlertDeliveries.Add(delivery);
        await store.DbContext.SaveChangesAsync();
        return delivery;
    }

    /// <summary>
    /// The seeded entities of one alert with two recipients.
    /// </summary>
    /// <param name="Alert">The alert.</param>
    /// <param name="Server">The server the alert is about.</param>
    /// <param name="First">The first recipient, also the cost owner user.</param>
    /// <param name="Second">The second recipient.</param>
    private sealed record AlertSeed(CostAlert Alert, GameServer Server, User First, User Second);

    /// <summary>
    /// A logger that collects every formatted message and exception text so tests can search the output.
    /// </summary>
    private sealed class CapturingLogger : ILogger<CostAlertDeliveryProcessor>
    {
        private readonly List<string> _entries = [];

        /// <summary>
        /// Gets everything that was logged, joined into one string.
        /// </summary>
        /// <value>All formatted messages and exception texts, one per line.</value>
        public string Output
        {
            get
            {
                lock (_entries)
                    return string.Join('\n', _entries);
            }
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception) + (exception is null ? string.Empty : "\n" + exception);
            lock (_entries)
                _entries.Add(text);
        }
    }
}
