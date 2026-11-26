using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// T-66-14 proof that accrued values, new alerts and dismissals reach only the users who may see the cost owner, that
/// an audience failure sends nothing and logs one warning, and that the frames serialize through the real update
/// stream with ISO times and numeric amounts. The tests run the real <see cref="CostAlertPublisher"/> and
/// <see cref="HostingAuthorizationService"/> audiences against in-memory SQLite.
/// </summary>
public sealed class CostAlertPublisherTests
{
    /// <summary>
    /// The instant every accrual in these tests is valid for.
    /// </summary>
    private static readonly Instant AsOf = Instant.FromUnixTimeSeconds(1_700_000_900);

    /// <summary>
    /// One frame per costing deployment goes to each user who may view the server and the cost owner; a viewer of the
    /// server without owner visibility and a finance viewer without server access receive nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Accruals_GoToServerAndOwnerViewersOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await GrantAsync(store, seed.User, Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        var serverViewer = await AddUserAsync(store, "server-viewer");
        await GrantAsync(store, serverViewer, Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        var financeViewer = await AddUserAsync(store, "finance-viewer");
        await GrantAsync(store, financeViewer, Claims.Finance.ViewAll, string.Empty);
        var sender = new RecordingUpdateStream();

        await CreatePublisher(store, sender).PublishAccrualsAsync([CreateAccrual(seed, isCosting: true)], default);

        var frame = Assert.Single(sender.Sent);
        Assert.Equal(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), frame.Path);
        var message = Assert.IsType<GameServerAccruedCostMessage>(frame.Package);
        Assert.Equal(seed.Server.PrimaryKey, message.ServerId);
        Assert.Equal(seed.Deployment!.PrimaryKey, message.DeploymentId);
        Assert.Equal(3.5m, message.Amount);
        Assert.Equal("EUR", message.Currency);
        Assert.Equal(AsOf.ToDateTimeOffset(), message.AsOf);
        Assert.False(message.IsFinal);
        Assert.Null(message.UnavailableReason);
    }

    /// <summary>
    /// A Hetzner accrual publishes the one-hour billing unit and the billed-through instant to the same audience, and
    /// both fields serialize through the real update stream as a number and an ISO-8601 string with offset; an
    /// accrual whose amount is unavailable publishes both as null.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task AccruedFrame_CarriesBillingUnitAndBilledThroughOnlyWithTheAmount()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await GrantAsync(store, seed.User, Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        var strangerViewer = await AddUserAsync(store, "server-viewer");
        await GrantAsync(store, strangerViewer, Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        var billedThrough = AsOf + Duration.FromMinutes(40);
        var withAmount = new AccruedCostResult(
            3.5m, AsOf, false, null, billedThrough, RuntimeBillingUnit.StartedHour);
        var withoutAmount = new AccruedCostResult(
            null, AsOf, false, AccruedCostUnavailableReasons.Rate, billedThrough, RuntimeBillingUnit.StartedHour);
        var sender = new RecordingUpdateStream();

        await CreatePublisher(store, sender).PublishAccrualsAsync(
            [CreateAccrual(seed, isCosting: true, withAmount), CreateAccrual(seed, isCosting: true, withoutAmount)],
            default);

        Assert.Equal(2, sender.Sent.Count);
        Assert.All(
            sender.Sent,
            sent => Assert.Equal(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), sent.Path));
        var visible = Assert.IsType<GameServerAccruedCostMessage>(sender.Sent[0].Package);
        Assert.Equal(3600, visible.BillingUnitSeconds);
        Assert.Equal(billedThrough.ToDateTimeOffset(), visible.BilledThrough);
        var unavailable = Assert.IsType<GameServerAccruedCostMessage>(sender.Sent[1].Package);
        Assert.Null(unavailable.Amount);
        Assert.Null(unavailable.BillingUnitSeconds);
        Assert.Null(unavailable.BilledThrough);

        await using var stream = new UpdateStreamService();
        var socket = new CapturingWebSocket();
        using var cancellation = new CancellationTokenSource();
        var registration = stream
            .RegisterAsync(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), socket, cancellation.Token)
            .AsTask();
        await CreatePublisher(store, stream).PublishAccrualsAsync([CreateAccrual(seed, true, withAmount)], default);
        var json = Assert.Single(socket.Frames);
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement.GetProperty("value");
        Assert.Equal(JsonValueKind.Number, value.GetProperty("billingUnitSeconds").ValueKind);
        Assert.Equal(3600, value.GetProperty("billingUnitSeconds").GetInt32());
        var through = value.GetProperty("billedThrough");
        Assert.Equal(JsonValueKind.String, through.ValueKind);
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$"), through.GetString()!);
        Assert.Equal(billedThrough.ToDateTimeOffset(), through.GetDateTimeOffset());
        socket.Close();
        await registration;
    }

    /// <summary>
    /// A deployment that no longer costs sends nothing, even to users in both audiences.
    /// </summary>
    /// <returns>A task completing once the assertion ran.</returns>
    [Fact]
    public async Task ResolvedAccrual_SendsNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await GrantAsync(store, seed.User, Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        var sender = new RecordingUpdateStream();

        await CreatePublisher(store, sender).PublishAccrualsAsync([CreateAccrual(seed, isCosting: false)], default);

        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// A new alert goes to the whole owner audience, including a finance viewer without server access, and carries
    /// every field of the alert.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task RaisedAlert_GoesToOwnerAudienceIncludingFinanceViewerWithoutServerAccess()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var financeViewer = await AddUserAsync(store, "finance-viewer");
        await GrantAsync(store, financeViewer, Claims.Finance.ViewAll, string.Empty);
        var stranger = await AddUserAsync(store, "stranger");
        var sender = new RecordingUpdateStream();
        var alertId = Guid.NewGuid();
        var raised = new RaisedCostAlert(
            alertId, seed.Server.PrimaryKey, "Integration server", seed.CostOwner.PrimaryKey, 5m, 5.25m, "EUR", AsOf);

        await CreatePublisher(store, sender).PublishRaisedAsync([raised], default);

        Assert.Equal(
            new[]
            {
                FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey),
                FleetChangeStreamPaths.ForUser(financeViewer.PrimaryKey),
            }.Order().ToArray(),
            sender.Sent.Select(q => q.Path).Order().ToArray());
        Assert.DoesNotContain(
            sender.Sent,
            q => q.Path == FleetChangeStreamPaths.ForUser(stranger.PrimaryKey));
        var message = Assert.IsType<CostAlertRaisedMessage>(sender.Sent[0].Package);
        Assert.Equal(alertId, message.AlertId);
        Assert.Equal(seed.Server.PrimaryKey, message.ServerId);
        Assert.Equal("Integration server", message.ServerName);
        Assert.Equal(seed.CostOwner.PrimaryKey, message.CostOwnerId);
        Assert.Equal(5m, message.ThresholdAmount);
        Assert.Equal(5.25m, message.AccruedAmount);
        Assert.Equal("EUR", message.Currency);
        Assert.Equal(AsOf.ToDateTimeOffset(), message.CreatedAt);
    }

    /// <summary>
    /// When the authorization service throws while reading an audience, nothing is sent and exactly one warning is
    /// logged, for accrued values and for alerts alike.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task AudienceFailure_SendsNothing_AndLogsOneWarning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var sender = new RecordingUpdateStream();
        var logger = new RecordingLogger();
        var publisher = new CostAlertPublisher(sender, new ThrowingAuthorizationService(), logger);

        await publisher.PublishAccrualsAsync([CreateAccrual(seed, isCosting: true)], default);
        Assert.Empty(sender.Sent);
        Assert.Equal(1, logger.WarningCount);

        await publisher.PublishRaisedAsync(
            [new RaisedCostAlert(Guid.NewGuid(), seed.Server.PrimaryKey, "x", seed.CostOwner.PrimaryKey, 1m, 1m, "EUR", AsOf)],
            default);
        Assert.Empty(sender.Sent);
        Assert.Equal(2, logger.WarningCount);
    }

    /// <summary>
    /// A dismissal frame goes only to the dismissing viewer's own path, one per alert id.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Dismissed_GoesToCallerPathOnly_OnePerAlert()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var sender = new RecordingUpdateStream();
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid()];

        await CreatePublisher(store, sender).PublishDismissedAsync(seed.User.PrimaryKey, ids, default);

        Assert.All(sender.Sent, q => Assert.Equal(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), q.Path));
        Assert.Equal(
            ids.Order().ToArray(),
            sender.Sent.Select(q => Assert.IsType<CostAlertDismissedMessage>(q.Package).AlertId).Order().ToArray());
    }

    /// <summary>
    /// A failing send to one recipient is logged and does not stop the frames for the other recipients.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FailingSend_DoesNotStopOtherRecipients()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var financeViewer = await AddUserAsync(store, "finance-viewer");
        await GrantAsync(store, financeViewer, Claims.Finance.ViewAll, string.Empty);
        var sender = new RecordingUpdateStream { FailPath = FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey) };
        var logger = new RecordingLogger();
        var publisher = new CostAlertPublisher(sender, new HostingAuthorizationService(store.DbContext), logger);

        await publisher.PublishRaisedAsync(
            [new RaisedCostAlert(Guid.NewGuid(), seed.Server.PrimaryKey, "x", seed.CostOwner.PrimaryKey, 1m, 1m, "EUR", AsOf)],
            default);

        Assert.Equal(FleetChangeStreamPaths.ForUser(financeViewer.PrimaryKey), Assert.Single(sender.Sent).Path);
        Assert.Equal(1, logger.WarningCount);
    }

    /// <summary>
    /// An AutoStop alert is pushed with its kind, stop time, reason and limit, while a threshold alert pushes the
    /// threshold kind and no stop facts.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task RaisedAutoStopAlert_CarriesKindAndStopFacts_ThresholdAlertDoesNot()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var sender = new RecordingUpdateStream();
        var stopsAt = AsOf + Duration.FromMinutes(5);
        var autoStop = new RaisedCostAlert(
            Guid.NewGuid(),
            seed.Server.PrimaryKey,
            "Integration server",
            seed.CostOwner.PrimaryKey,
            0m,
            0m,
            "EUR",
            AsOf,
            ECostAlertKind.AutoStop,
            stopsAt,
            EAutoStopReason.Idle,
            30);
        var threshold = new RaisedCostAlert(
            Guid.NewGuid(), seed.Server.PrimaryKey, "Integration server", seed.CostOwner.PrimaryKey, 5m, 5m, "EUR", AsOf);

        await CreatePublisher(store, sender).PublishRaisedAsync([autoStop, threshold], default);

        var messages = sender.Sent.Select(q => Assert.IsType<CostAlertRaisedMessage>(q.Package)).ToArray();
        var stop = Assert.Single(messages, q => q.AlertId == autoStop.CostAlertId);
        Assert.Equal("AutoStop", stop.Kind);
        Assert.Equal(stopsAt.ToDateTimeOffset(), stop.StopsAt);
        Assert.Equal("Idle", stop.StopReason);
        Assert.Equal(30, stop.StopLimit);
        var plain = Assert.Single(messages, q => q.AlertId == threshold.CostAlertId);
        Assert.Equal("CostThreshold", plain.Kind);
        Assert.Null(plain.StopsAt);
        Assert.Null(plain.StopReason);
        Assert.Null(plain.StopLimit);
    }

    /// <summary>
    /// A resolved warning sends one dismissal frame per alert to every user of the cost owner's view audience.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Resolved_SendsOneDismissalPerAlertToEveryOwnerAudienceUser()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var financeViewer = await AddUserAsync(store, "finance-viewer");
        await GrantAsync(store, financeViewer, Claims.Finance.ViewAll, string.Empty);
        var stranger = await AddUserAsync(store, "stranger");
        var sender = new RecordingUpdateStream();
        var resolved = new ResolvedCostAlert(Guid.NewGuid(), seed.Server.PrimaryKey, seed.CostOwner.PrimaryKey);

        await CreatePublisher(store, sender).PublishResolvedAsync([resolved], default);

        Assert.Equal(
            new[]
            {
                FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey),
                FleetChangeStreamPaths.ForUser(financeViewer.PrimaryKey),
            }.Order().ToArray(),
            sender.Sent.Select(q => q.Path).Order().ToArray());
        Assert.DoesNotContain(sender.Sent, q => q.Path == FleetChangeStreamPaths.ForUser(stranger.PrimaryKey));
        Assert.All(
            sender.Sent,
            q => Assert.Equal(resolved.CostAlertId, Assert.IsType<CostAlertDismissedMessage>(q.Package).AlertId));
    }

    /// <summary>
    /// A failing audience read and a failing send are swallowed and logged, never thrown.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Resolved_SwallowsAudienceAndSendFailures()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var resolved = new ResolvedCostAlert(Guid.NewGuid(), seed.Server.PrimaryKey, seed.CostOwner.PrimaryKey);
        var sender = new RecordingUpdateStream { FailPath = FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey) };
        var logger = new RecordingLogger();

        await new CostAlertPublisher(sender, new ThrowingAuthorizationService(), logger)
            .PublishResolvedAsync([resolved], default);
        Assert.Empty(sender.Sent);
        Assert.Equal(1, logger.WarningCount);

        await new CostAlertPublisher(sender, new HostingAuthorizationService(store.DbContext), logger)
            .PublishResolvedAsync([resolved], default);
        Assert.Empty(sender.Sent);
        Assert.Equal(2, logger.WarningCount);
    }

    /// <summary>
    /// Serialized through the real update stream, the accrued frame carries an ISO-8601 time with offset, a numeric
    /// amount and the fully qualified wire type.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task AccruedFrame_SerializesWithIsoTimeNumericAmountAndWireType()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await GrantAsync(store, seed.User, Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        await using var stream = new UpdateStreamService();
        var socket = new CapturingWebSocket();
        using var cancellation = new CancellationTokenSource();
        var registration = stream
            .RegisterAsync(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), socket, cancellation.Token)
            .AsTask();

        await CreatePublisher(store, stream).PublishAccrualsAsync([CreateAccrual(seed, isCosting: true)], default);

        var json = Assert.Single(socket.Frames);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerAccruedCostMessage",
            root.GetProperty("type").GetString());
        var value = root.GetProperty("value");
        Assert.Equal(JsonValueKind.Number, value.GetProperty("amount").ValueKind);
        Assert.Equal(3.5m, value.GetProperty("amount").GetDecimal());
        var asOf = value.GetProperty("asOf");
        Assert.Equal(JsonValueKind.String, asOf.ValueKind);
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$"), asOf.GetString()!);
        Assert.Equal(AsOf.ToDateTimeOffset(), asOf.GetDateTimeOffset());
        socket.Close();
        await registration;
    }

    /// <summary>
    /// Creates the publisher under test on real authorization audiences of the store.
    /// </summary>
    /// <param name="store">The store the audiences are read from.</param>
    /// <param name="sender">The update stream the frames are sent through.</param>
    /// <returns>The publisher under test.</returns>
    private static CostAlertPublisher CreatePublisher(HostingTestStore store, IUpdateStreamService sender)
        => new(sender, new HostingAuthorizationService(store.DbContext), new RecordingLogger());

    /// <summary>
    /// Creates the accrual of the seeded deployment.
    /// </summary>
    /// <param name="seed">The seeded server and deployment.</param>
    /// <param name="isCosting">Whether the deployment still costs.</param>
    /// <param name="result">The calculated result, or <see langword="null"/> for an amount of 3.5 without a unit.</param>
    /// <returns>The accrual carrying <paramref name="result"/>.</returns>
    private static DeploymentAccrual CreateAccrual(
        HostingTestSeed seed,
        bool isCosting,
        AccruedCostResult? result = null)
        => new(
            seed.Deployment!.PrimaryKey,
            seed.Server.PrimaryKey,
            seed.CostOwner.PrimaryKey,
            isCosting,
            result ?? new AccruedCostResult(3.5m, AsOf, !isCosting, null),
            "EUR");

    /// <summary>
    /// Adds and saves an active user without claims.
    /// </summary>
    /// <param name="store">The store the user is added to.</param>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>A task resolving to the saved user.</returns>
    private static async Task<User> AddUserAsync(HostingTestStore store, string nickname)
    {
        var user = new User
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord        = new DiscordUser { Username = nickname },
            Steam          = new SteamUser(),
        };
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Grants a claim directly to a user.
    /// </summary>
    /// <param name="store">The store the claim is saved in.</param>
    /// <param name="user">The user receiving the claim.</param>
    /// <param name="identifier">The claim identifier.</param>
    /// <param name="value">The claim value; empty for a global claim.</param>
    /// <returns>A task completing once the claim is saved.</returns>
    private static async Task GrantAsync(HostingTestStore store, User user, string identifier, string value)
    {
        var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            store.DbContext.Claims,
            q => q.Identifier == identifier && q.Value == value && q.ValueType == null);
        user.Claims!.Add(existing ?? new X39.UnitedTacticalForces.Database.Authority.Claim
        {
            Identifier  = identifier,
            Value       = value,
            Category    = "test",
            Title       = identifier,
            Description = "test claim",
        });
        await store.DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// An update stream double that records every frame and can fail the send to one path.
    /// </summary>
    private sealed class RecordingUpdateStream : IUpdateStreamService
    {
        /// <summary>
        /// Gets the frames sent so far.
        /// </summary>
        /// <value>One entry per successful send, in send order.</value>
        public List<(string Path, object? Package)> Sent { get; } = [];

        /// <summary>
        /// Gets the path whose sends throw, when set.
        /// </summary>
        /// <value><see langword="null"/> when every send succeeds.</value>
        public string? FailPath { get; init; }

        /// <inheritdoc />
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask SendUpdateAsync<T>(string path, T package)
        {
            if (path == FailPath)
                throw new IOException("send failed");
            Sent.Add((path, package));
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A logger double that counts warnings.
    /// </summary>
    private sealed class RecordingLogger : ILogger<CostAlertPublisher>
    {
        /// <summary>
        /// Gets the number of warnings logged.
        /// </summary>
        /// <value>The count of entries at <see cref="LogLevel.Warning"/>.</value>
        public int WarningCount { get; private set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
            if (logLevel == LogLevel.Warning)
                WarningCount++;
        }
    }

    /// <summary>
    /// An authorization double whose audience reads throw and whose other members are unused.
    /// </summary>
    private sealed class ThrowingAuthorizationService : IHostingAuthorizationService
    {
        /// <inheritdoc />
        public bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<bool> CanUseCostOwnerAsync(
            ClaimsPrincipal principal,
            Guid costOwnerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<bool> CanViewCostOwnerAsync(
            ClaimsPrincipal principal,
            Guid costOwnerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<ClaimsPrincipal?> RebuildPrincipalAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetServerViewAudienceAsync(
            IReadOnlyCollection<Guid> gameServerIds,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("audience read failed");

        /// <inheritdoc />
        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCostOwnerViewAudienceAsync(
            IReadOnlyCollection<Guid> costOwnerIds,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("audience read failed");

        /// <inheritdoc />
        public Task<IReadOnlySet<Guid>?> GetViewableCostOwnerIdsAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// A WebSocket double that captures text frames and ends its receive loop when the test closes it.
    /// </summary>
    private sealed class CapturingWebSocket : WebSocket
    {
        /// <summary>
        /// Completed by <see cref="Close"/> to end the receive loop.
        /// </summary>
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets the captured frames as UTF-8 text.
        /// </summary>
        /// <value>One entry per sent frame.</value>
        public List<string> Frames { get; } = [];

        /// <summary>
        /// Ends the receive loop of the registered socket.
        /// </summary>
        public void Close() => _closed.TrySetResult();

        /// <inheritdoc />
        public override WebSocketCloseStatus? CloseStatus => null;

        /// <inheritdoc />
        public override string? CloseStatusDescription => null;

        /// <inheritdoc />
        public override WebSocketState State => WebSocketState.Open;

        /// <inheritdoc />
        public override string? SubProtocol => null;

        /// <inheritdoc />
        public override void Abort()
        {
        }

        /// <inheritdoc />
        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Dispose()
        {
        }

        /// <inheritdoc />
        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            await _closed.Task.ConfigureAwait(false);
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        /// <inheritdoc />
        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            Frames.Add(Encoding.UTF8.GetString(buffer));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            Frames.Add(Encoding.UTF8.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }
    }
}
