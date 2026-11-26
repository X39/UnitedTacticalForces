using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that metrics reach storage and the browser only through authenticated <c>Metrics</c> frames on the worker
/// channel: a primary worker's sample is stored and pushed to the server's viewers, everything else is discarded or
/// rejected, and an audience failure never fails the acknowledgement. The tests run the real channel handler over a
/// loopback socket with the real metrics service, publisher and audience rules.
/// </summary>
public sealed class WorkerChannelMetricsTests
{
    private const long GiB = 1024L * 1024 * 1024;

    /// <summary>
    /// A frame from the primary worker is acknowledged, creates the snapshot and the three bucket rows, and reaches a
    /// viewer of the server on the viewer's own path with the readings and the stale limit; a user without the view
    /// claim receives nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task PrimaryFrame_IsStoredAndPushedToTheViewersOnly()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await MarkProcessRunningAsync(host, worker);
        var viewer = await GrantViewerAsync(host, worker, "viewer");
        await AddUserAsync(host, "stranger");
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(
            worker.DeploymentId,
            cpu: 30,
            used: 2 * GiB,
            total: 7 * GiB,
            players: 3,
            capacity: 16));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        Assert.Equal(1, await host.Store.DbContext.ServerMetricSnapshots.CountAsync());
        Assert.Equal(3, await host.Store.DbContext.ServerMetricBuckets.CountAsync());
        var frame = Assert.Single(sender.Sent);
        Assert.Equal(FleetChangeStreamPaths.ForUser(viewer.PrimaryKey), frame.Path);
        var message = Assert.IsType<GameServerMetricsMessage>(frame.Package);
        Assert.Equal(worker.GameServerId, message.ServerId);
        Assert.Equal(45, message.StaleAfterSeconds);
        Assert.NotNull(message.AsOf);
        Assert.NotNull(message.RunningSince);
        Assert.Equal(30, message.CpuPercent);
        Assert.Null(message.CpuUnavailableReason);
        Assert.Equal(2 * GiB, message.MemoryUsedBytes);
        Assert.Equal(7 * GiB, message.MemoryTotalBytes);
        Assert.Equal(3, message.PlayerCount);
        Assert.Equal(16, message.PlayerCapacity);
        Assert.Null(message.PlayersUnavailableReason);
    }

    /// <summary>
    /// A failing audience read logs exactly one warning, sends nothing and still lets the frame be acknowledged and
    /// stored, so a metrics push can never fail the worker's acknowledgement.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FailingAudienceRead_SendsNothingAndStillAcknowledges()
    {
        var sender = new RecordingUpdateStream();
        var logger = new RecordingLogger();
        await using var host = await CreateHostAsync(sender, logger, throwingAudience: true);
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(worker.DeploymentId, cpu: 30, used: GiB, total: 4 * GiB, players: 1));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        Assert.Empty(sender.Sent);
        Assert.Equal(1, logger.WarningCount);
        Assert.Equal(1, await host.Store.DbContext.ServerMetricSnapshots.CountAsync());
    }

    /// <summary>
    /// A frame from a headless-client worker of the same deployment is acknowledged, stores nothing and publishes
    /// nothing, so a multi-host deployment never mixes machines into one reading.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task HeadlessClientFrame_IsAcknowledgedAndDiscarded()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        var (primary, _, _) = await host.SeedRegisteredWorkerAsync();
        await GrantViewerAsync(host, primary, "viewer");
        var headless = await RegisterHeadlessWorkerAsync(host, primary);
        host.AdoptSeededWorker(headless);
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(
            headless.Worker.DeploymentId,
            cpu: 55,
            used: GiB,
            total: 4 * GiB,
            players: 2));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        Assert.False(headless.Worker.IsPrimaryHost);
        Assert.Empty(sender.Sent);
        Assert.Equal(0, await host.Store.DbContext.ServerMetricSnapshots.CountAsync());
        Assert.Equal(0, await host.Store.DbContext.ServerMetricBuckets.CountAsync());
    }

    /// <summary>
    /// A frame whose payload names another deployment is rejected and stores nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FrameForAnotherDeployment_IsRejectedAndStoresNothing()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(Guid.NewGuid(), cpu: 30, used: GiB, total: 4 * GiB, players: 1));

        await AssertRejectedAndEmptyAsync(host, sender, ack);
    }

    /// <summary>
    /// A frame whose CPU carries both a value and a reason is rejected and stores nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FrameWithAValueAndAReason_IsRejectedAndStoresNothing()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(
            worker.DeploymentId,
            cpu: 30,
            used: GiB,
            total: 4 * GiB,
            players: 1,
            cpuReason: WorkerMetricUnavailableReasons.QueryFailed));

        await AssertRejectedAndEmptyAsync(host, sender, ack);
    }

    /// <summary>
    /// A frame sampled two minutes in the future is rejected and stores nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FrameSampledInTheFuture_IsRejectedAndStoresNothing()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(
            worker.DeploymentId,
            cpu: 30,
            used: GiB,
            total: 4 * GiB,
            players: 1,
            sampledAt: DateTimeOffset.UtcNow.AddMinutes(2)));

        await AssertRejectedAndEmptyAsync(host, sender, ack);
    }

    /// <summary>
    /// A frame without a payload is rejected and stores nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FrameWithoutPayload_IsRejectedAndStoresNothing()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(new WorkerChannelFrame
        {
            Type = EWorkerChannelFrameType.Metrics,
            Id = Guid.NewGuid(),
        });

        await AssertRejectedAndEmptyAsync(host, sender, ack);
    }

    /// <summary>
    /// A frame sent after the worker credential was revoked closes the socket with a policy violation, stores nothing
    /// and is never acknowledged.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task FrameAfterCredentialRevocation_ClosesTheChannelWithPolicyViolation()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();
        var row = await host.Store.DbContext.Set<LifecycleWorker>().SingleAsync(q => q.PrimaryKey == worker.WorkerId);
        row.WorkerCredentialRevokedAt = NodaSystemClock.Instance.GetCurrentInstant();
        await host.Store.DbContext.SaveChangesAsync();

        await client.SendAsync(MetricsFrame(worker.DeploymentId, cpu: 30, used: GiB, total: 4 * GiB, players: 1));
        var status = await client.ReceiveCloseAsync();

        Assert.Equal(WebSocketCloseStatus.PolicyViolation, status);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(sender.Sent);
        Assert.Equal(0, await host.Store.DbContext.ServerMetricSnapshots.CountAsync());
    }

    /// <summary>
    /// A sample whose player reason says the game does not report players is pushed with a null count and that
    /// reason, never with zero.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task GameDoesNotReportPlayers_IsPushedAsAReasonNeverZero()
    {
        var sender = new RecordingUpdateStream();
        await using var host = await CreateHostAsync(sender);
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await MarkProcessRunningAsync(host, worker);
        await GrantViewerAsync(host, worker, "viewer");
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(MetricsFrame(
            worker.DeploymentId,
            cpu: 12,
            used: GiB,
            total: 4 * GiB,
            players: null,
            playersReason: WorkerMetricUnavailableReasons.GameDoesNotReport));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        var message = Assert.IsType<GameServerMetricsMessage>(Assert.Single(sender.Sent).Package);
        Assert.Null(message.PlayerCount);
        Assert.Equal(WorkerMetricUnavailableReasons.GameDoesNotReport, message.PlayersUnavailableReason);
        Assert.Equal(12, message.CpuPercent);
    }

    /// <summary>
    /// Asserts that a frame was rejected, nothing was stored and nothing was pushed.
    /// </summary>
    /// <param name="host">The host whose store is inspected.</param>
    /// <param name="sender">The update stream that must not have sent anything.</param>
    /// <param name="ack">The acknowledgement received for the frame.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    private static async Task AssertRejectedAndEmptyAsync(
        WorkerChannelTestHost host,
        RecordingUpdateStream sender,
        WorkerChannelFrame ack)
    {
        Assert.Equal(EWorkerChannelOutcome.Rejected, ack.Outcome);
        Assert.Empty(sender.Sent);
        Assert.Equal(0, await host.Store.DbContext.ServerMetricSnapshots.CountAsync());
        Assert.Equal(0, await host.Store.DbContext.ServerMetricBuckets.CountAsync());
    }

    /// <summary>
    /// Creates a channel host with the metrics route, service and publisher registered.
    /// </summary>
    /// <param name="sender">The update stream double that records the pushed frames.</param>
    /// <param name="logger">The publisher logger double, or <see langword="null"/> for the default logger.</param>
    /// <param name="throwingAudience">Whether the audience read of the publisher fails.</param>
    /// <returns>A task resolving to the host.</returns>
    private static Task<WorkerChannelTestHost> CreateHostAsync(
        RecordingUpdateStream sender,
        RecordingLogger? logger = null,
        bool throwingAudience = false)
        => WorkerChannelTestHost.CreateAsync(configure: services =>
        {
            services.AddSingleton(Options.Create(new ServerMetricsOptions()));
            services.AddScoped<IHostingAuthorizationService>(q => throwingAudience
                ? new ThrowingAudienceAuthorizationService()
                : new HostingAuthorizationService(q.GetRequiredService<DatabaseContext>()));
            services.AddScoped<IServerMetricsService, ServerMetricsService>();
            services.AddScoped<IServerMetricsPublisher, ServerMetricsPublisher>();
            services.AddScoped<IWorkerChannelFrameRoute, MetricsFrameRoute>();
            services.AddSingleton<IUpdateStreamService>(sender);
            if (logger is not null)
                services.AddSingleton<ILogger<ServerMetricsPublisher>>(logger);
        });

    /// <summary>
    /// Builds a metrics frame whose payload is a sample with exactly the given readings.
    /// </summary>
    /// <param name="deploymentId">The deployment the payload names.</param>
    /// <param name="cpu">The CPU percentage, or <see langword="null"/> when unavailable.</param>
    /// <param name="used">The used memory in bytes, or <see langword="null"/> when unavailable.</param>
    /// <param name="total">The total memory in bytes, or <see langword="null"/> when unavailable.</param>
    /// <param name="players">The player count, or <see langword="null"/> when unavailable.</param>
    /// <param name="capacity">The player capacity, or <see langword="null"/> when unknown.</param>
    /// <param name="cpuReason">The CPU reason, if any.</param>
    /// <param name="playersReason">The players reason, if any.</param>
    /// <param name="sampledAt">The worker sampling time, or <see langword="null"/> for now.</param>
    /// <returns>The frame with a fresh request id.</returns>
    private static WorkerChannelFrame MetricsFrame(
        Guid deploymentId,
        double? cpu,
        long? used,
        long? total,
        int? players,
        int? capacity = null,
        string? cpuReason = null,
        string? playersReason = null,
        DateTimeOffset? sampledAt = null)
        => new()
        {
            Type = EWorkerChannelFrameType.Metrics,
            Id = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(
                ServerMetricsTestData.Sample(
                    deploymentId,
                    sampledAt ?? DateTimeOffset.UtcNow,
                    cpu,
                    used,
                    total,
                    players,
                    capacity,
                    cpuReason,
                    playersReason: playersReason),
                WorkerChannelJson.Options),
        };

    /// <summary>
    /// Records a running game process on the seeded primary worker so process-bound readings are available.
    /// </summary>
    /// <param name="host">The host whose store is updated.</param>
    /// <param name="worker">The seeded primary worker.</param>
    /// <returns>A task completing once the process start is saved.</returns>
    private static async Task MarkProcessRunningAsync(WorkerChannelTestHost host, AuthenticatedWorker worker)
    {
        var row = await host.Store.DbContext.Set<LifecycleWorker>().SingleAsync(q => q.PrimaryKey == worker.WorkerId);
        row.ProcessStartedAt = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(5);
        await host.Store.DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Registers a second worker of the same deployment that hosts a headless client instead of the game server.
    /// </summary>
    /// <param name="host">The host whose protocol service registers the worker.</param>
    /// <param name="primary">The seeded primary worker whose deployment is shared.</param>
    /// <returns>The authenticated identity and credential of the headless worker.</returns>
    private static async Task<(AuthenticatedWorker Worker, string Credential, Guid CommandId)>
        RegisterHeadlessWorkerAsync(WorkerChannelTestHost host, AuthenticatedWorker primary)
    {
        var dbContext = host.Store.DbContext;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var row = new LifecycleWorker
        {
            PrimaryKey        = Guid.NewGuid(),
            DeploymentFk      = primary.DeploymentId,
            ServiceIdentifier = "headless-client",
            ServiceOrdinal    = 1,
            Identity          = "pending",
            Status            = EWorkerStatus.Provisioning,
            TimeStampCreated  = now,
            TimeStampUpdated  = now,
        };
        var bootstrap = host.Services.GetRequiredService<IWorkerCredentialService>().IssueBootstrapCredential(row);
        dbContext.LifecycleWorkers.Add(row);
        await dbContext.SaveChangesAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var registration = await scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>().RegisterAsync(
            row.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = primary.DeploymentId,
                Identity = "headless-test-worker",
                ProtocolVersion = 3,
                SupportedGameIdentifiers = [HostingTestData.GameIdentifier],
            },
            bootstrap.BootstrapCredential);
        var identity = new AuthenticatedWorker(
            row.PrimaryKey,
            primary.DeploymentId,
            primary.GameServerId,
            row.ServiceIdentifier,
            row.ServiceOrdinal);
        return (identity, registration.WorkerCredential, Guid.Empty);
    }

    /// <summary>
    /// Adds an active user without claims.
    /// </summary>
    /// <param name="host">The host whose store receives the user.</param>
    /// <param name="nickname">The nickname and Discord name of the user.</param>
    /// <returns>A task resolving to the saved user.</returns>
    private static async Task<User> AddUserAsync(WorkerChannelTestHost host, string nickname)
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
        host.Store.DbContext.Users.Add(user);
        await host.Store.DbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Adds a user who holds the view claim of the worker's server.
    /// </summary>
    /// <param name="host">The host whose store receives the user and the claim.</param>
    /// <param name="worker">The worker whose server the user may view.</param>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>A task resolving to the saved user.</returns>
    private static async Task<User> GrantViewerAsync(
        WorkerChannelTestHost host,
        AuthenticatedWorker worker,
        string nickname)
    {
        var user = await AddUserAsync(host, nickname);
        user.Claims!.Add(new X39.UnitedTacticalForces.Database.Authority.Claim
        {
            Identifier  = Claims.ResourceBased.Server.View,
            Value       = worker.GameServerId.ToString(),
            Category    = "test",
            Title       = Claims.ResourceBased.Server.View,
            Description = "test claim",
        });
        await host.Store.DbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// An update stream double that records every frame it is asked to send.
    /// </summary>
    private sealed class RecordingUpdateStream : IUpdateStreamService
    {
        /// <summary>
        /// Gets the frames sent so far.
        /// </summary>
        /// <value>One entry per send, in send order.</value>
        public List<(string Path, object? Package)> Sent { get; } = [];

        /// <inheritdoc />
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask SendUpdateAsync<T>(string path, T package)
        {
            Sent.Add((path, package));
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A publisher logger double that counts warnings.
    /// </summary>
    private sealed class RecordingLogger : ILogger<ServerMetricsPublisher>
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
    private sealed class ThrowingAudienceAuthorizationService : IHostingAuthorizationService
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
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlySet<Guid>?> GetViewableCostOwnerIdsAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
