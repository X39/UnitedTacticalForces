using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Emulates the dedicated worker of every seeded server listed in <see cref="UatFleetScenarioSeeder.EmulatedWorkers"/>
/// so manual UAT sees live tiles and a growing one hour chart without a real cloud machine. It also acknowledges stop
/// commands, so a Stop job of an emulated server completes on the UAT stack. Each server gets its own session with its
/// own player feed.
/// </summary>
/// <remarks>
/// <para>
/// This service exists only for manual UAT. It executes no game process and writes no metrics itself: it issues a fresh
/// worker credential, authenticates like a real worker, and then sends Heartbeat and Metrics frames from the client end
/// of a loopback WebSocket pair to the real <see cref="IWorkerChannelHandler"/>. Every reading therefore enters through
/// the worker channel (MET-05) and is stored, published and rate limited exactly like a real worker's reading.
/// </para>
/// <para>
/// The emulated worker also claims commands through the same channel, as a real worker does. A stop or fault-stop
/// command is reported received and succeeded with every assigned workload stopped; any other action is reported
/// failed with <see cref="UatEmulatedCommandReports.UnsupportedActionCode"/> instead of staying queued. After its stop
/// succeeded the session sends nothing more and ends, so the deployment can resolve. Every claim and report enters
/// through the real handler and protocol service; the emulation writes nothing itself.
/// </para>
/// <para>
/// The emulation ends by itself when the channel refuses it, for example after the server was shut down and the
/// deployment resolved. It is registered only in the Development environment with the UAT scenarios flag, see
/// <see cref="AddTo"/>, and does nothing while the hosting control plane is paused.
/// </para>
/// </remarks>
/// <param name="scopeFactory">Creates the scope used to find the seeded worker and to authenticate it.</param>
/// <param name="handler">The real worker channel handler the emulated worker talks to.</param>
/// <param name="state">The control plane state; an emulation never starts while it is paused.</param>
/// <param name="options">The timing of the emulation.</param>
/// <param name="logger">The logger; it never receives the credential.</param>
internal sealed class UatWorkerChannelEmulator(
    IServiceScopeFactory scopeFactory,
    IWorkerChannelHandler handler,
    HostingControlPlaneState state,
    IOptions<UatWorkerChannelEmulatorOptions> options,
    ILogger<UatWorkerChannelEmulator> logger) : BackgroundService
{
    /// <summary>The service identifier of the primary host worker.</summary>
    private const string PrimaryServiceIdentifier = "game-server";

    /// <summary>The stage the emulated worker reports in its heartbeats.</summary>
    private const string EmulatedStage = "uat-emulated";

    /// <summary>The player count the <see cref="EUatPlayerProfile.FewPlayers"/> profile reports.</summary>
    private const int FewPlayersCount = 3;

    /// <summary>The size of the buffer the emulated worker reads server frames into.</summary>
    private const int ReceiveChunkBytes = 4096;

    /// <summary>The largest server message the emulated worker assembles; larger messages are dropped.</summary>
    private const int MaxMessageBytes = 1024 * 1024;

    /// <summary>How long the emulation waits for the channel handler to end after the channel closed.</summary>
    private static readonly TimeSpan HandlerShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long an emulated worker waits for the acknowledgement of one request frame.</summary>
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(15);

    /// <summary>How long a stopped session waits for the handler to answer its close before it aborts the channel.</summary>
    private static readonly TimeSpan CloseGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the seeded servers that get an emulated worker session, keyed by server title, with their player feed.
    /// </summary>
    /// <value>
    /// <see cref="UatFleetScenarioSeeder.EmulatedWorkers"/> by default. The setter exists only as a test seam so tests
    /// can emulate more servers without touching the production map.
    /// </value>
    internal IReadOnlyDictionary<string, EUatPlayerProfile> EmulatedWorkers { get; init; }
        = UatFleetScenarioSeeder.EmulatedWorkers;

    /// <summary>
    /// Registers the emulator and its options when, and only when, the environment is Development and the UAT scenarios
    /// flag is set.
    /// </summary>
    /// <param name="services">The service collection that receives the registrations.</param>
    /// <param name="configuration">The configuration that holds the UAT scenarios flag and the emulator options.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns><see langword="true"/> when the emulator was registered.</returns>
    internal static bool AddTo(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(UatFleetScenarioSeeder.ConfigurationKey))
            return false;
        services.Configure<UatWorkerChannelEmulatorOptions>(
            configuration.GetSection(UatWorkerChannelEmulatorOptions.SectionName));
        services.AddHostedService<UatWorkerChannelEmulator>();
        return true;
    }

    /// <summary>
    /// Runs the emulated worker until the channel ends or the host stops. It never throws: a failure is logged and ends
    /// the emulation, because it is a development aid that must not stop the API.
    /// </summary>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the emulation ended.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "UAT worker emulation ended after a failure: {Failure}",
                HostingFailureSanitizer.Sanitize(ex.Message));
        }
    }

    /// <summary>
    /// Waits for the start delay, prepares the emulated worker and drives its channel.
    /// </summary>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the emulation ended.</returns>
    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        await Task.Delay(settings.StartDelay, stoppingToken).ConfigureAwait(false);
        if (state.IsPaused)
        {
            logger.LogInformation("UAT worker emulation is not started because the control plane is paused");
            return;
        }

        var sessions = new List<EmulatedSession>();
        var skippedTitles = new List<string>();
        foreach (var (title, profile) in EmulatedWorkers)
        {
            var session = await PrepareAsync(title, profile, stoppingToken).ConfigureAwait(false);
            if (session is null)
                skippedTitles.Add(title);
            else
                sessions.Add(session);
        }

        if (sessions.Count == 0)
        {
            logger.LogInformation(
                "UAT worker emulation found no running server with a primary worker among {Titles} and is not started",
                skippedTitles);
            return;
        }

        foreach (var title in skippedTitles)
        {
            logger.LogInformation(
                "UAT worker emulation found no running '{Title}' server with a primary worker and skips it",
                title);
        }

        await Task.WhenAll(sessions.Select(session => DriveChannelAsync(session, settings, stoppingToken)))
            .ConfigureAwait(false);
        logger.LogInformation("UAT worker emulation ended");
    }

    /// <summary>
    /// Finds the seeded worker of one server, issues it a fresh credential and authenticates it through the protocol
    /// service.
    /// </summary>
    /// <param name="title">The title of the seeded server to emulate the worker of.</param>
    /// <param name="profile">The player feed the session reports.</param>
    /// <param name="cancellationToken">A token that cancels the preparation.</param>
    /// <returns>
    /// The emulated worker session, or <see langword="null"/> when the server has no running deployment or primary
    /// worker.
    /// </returns>
    private async Task<EmulatedSession?> PrepareAsync(
        string title,
        EUatPlayerProfile profile,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var serverId = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.Title == title)
            .Select(q => (Guid?) q.PrimaryKey)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (serverId is not { } gameServerId)
            return null;
        var deploymentId = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null)
            .OrderByDescending(q => q.TimeStampCreated)
            .Select(q => (Guid?) q.PrimaryKey)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (deploymentId is not { } activeDeploymentId)
            return null;
        var worker = await dbContext.LifecycleWorkers
            .Where(q => q.DeploymentFk == activeDeploymentId
                        && q.ServiceIdentifier == PrimaryServiceIdentifier
                        && q.ServiceOrdinal == 0
                        && q.TimeStampStopped == null)
            .OrderByDescending(q => q.TimeStampCreated)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (worker is null)
            return null;

        var issue = scope.ServiceProvider.GetRequiredService<IWorkerCredentialService>()
            .IssuePendingWorkerCredential(worker, NodaSystemClock.Instance.GetCurrentInstant());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var identity = await scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>()
            .AuthenticateAsync(worker.PrimaryKey, activeDeploymentId, issue.Credential, cancellationToken)
            .ConfigureAwait(false);
        return new EmulatedSession(
            identity,
            issue.Credential,
            worker.ProcessId,
            worker.ProcessStartedAt?.ToDateTimeOffset(),
            profile);
    }

    /// <summary>
    /// Runs the real channel handler on the server end of a loopback pair and, from the client end, sends heartbeats and
    /// metrics, claims commands and reports them until a stop succeeded, the channel closes or the token is cancelled.
    /// </summary>
    /// <remarks>
    /// This method is the only sender of the session, so no two sends ever overlap on the client socket. The receive
    /// loop of <see cref="SessionChannel"/> only reads.
    /// </remarks>
    /// <param name="session">The authenticated emulated worker.</param>
    /// <param name="settings">The timing of the emulation.</param>
    /// <param name="stoppingToken">The token that signals host shutdown.</param>
    /// <returns>A task that completes when the session ended and the handler returned.</returns>
    private async Task DriveChannelAsync(
        EmulatedSession session,
        UatWorkerChannelEmulatorOptions settings,
        CancellationToken stoppingToken)
    {
        await using var pair = await UatLoopbackWebSocketPair.CreateAsync(stoppingToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var handlerTask = Task.Run(
            () => handler.RunAsync(session.Identity, session.Credential, pair.Server, linked.Token),
            CancellationToken.None);
        var channel = new SessionChannel(pair.Client);
        var receiveTask = channel.StartReceiving(linked.Token);
        var stopped = false;
        try
        {
            var nextHeartbeat = TimeSpan.Zero;
            var nextMetrics = TimeSpan.Zero;
            var nextClaim = TimeSpan.Zero;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!receiveTask.IsCompleted && !stoppingToken.IsCancellationRequested)
            {
                if (clock.Elapsed >= nextHeartbeat)
                {
                    await channel.SendAsync(HeartbeatFrame(session), linked.Token).ConfigureAwait(false);
                    nextHeartbeat = clock.Elapsed + settings.HeartbeatInterval;
                }

                if (clock.Elapsed >= nextMetrics)
                {
                    await channel.SendAsync(MetricsFrame(session), linked.Token).ConfigureAwait(false);
                    nextMetrics = clock.Elapsed + settings.MetricsInterval;
                }

                if (channel.TryConsumeWake() || clock.Elapsed >= nextClaim)
                {
                    nextClaim = clock.Elapsed + settings.ClaimPollInterval;
                    var (handled, stopSucceeded) = await ClaimAndReportAsync(session, channel, linked.Token)
                        .ConfigureAwait(false);
                    if (stopSucceeded)
                    {
                        stopped = true;
                        break;
                    }

                    // A fully reported command may be followed by another queued one, so claim again right away.
                    if (handled)
                        nextClaim = clock.Elapsed;
                    continue;
                }

                var next = nextHeartbeat < nextMetrics ? nextHeartbeat : nextMetrics;
                if (nextClaim < next)
                    next = nextClaim;
                var wait = next - clock.Elapsed;
                if (wait <= TimeSpan.Zero)
                    continue;
                using var delay = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                await Task.WhenAny(Task.Delay(wait, delay.Token), channel.WakeTask, receiveTask)
                    .ConfigureAwait(false);
                await delay.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException
                                       or InvalidOperationException)
        {
            // The channel closed or the host stopped while a frame was written; both end the emulation.
        }
        finally
        {
            if (stopped)
                await CloseStoppedSessionAsync(pair.Client, handlerTask).ConfigureAwait(false);
            await linked.CancelAsync().ConfigureAwait(false);
            await WaitQuietlyAsync(handlerTask).ConfigureAwait(false);
            await WaitQuietlyAsync(receiveTask).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits for a session task to end without letting a timeout or cancellation escape.
    /// </summary>
    /// <param name="task">The handler or receive task of a session whose token was cancelled.</param>
    /// <returns>A task that completes when the task ended or the shutdown timeout elapsed.</returns>
    private static async Task WaitQuietlyAsync(Task task)
    {
        try
        {
            await task.WaitAsync(HandlerShutdownTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Closes the client end of a session whose stop succeeded and gives the handler a moment to answer the close.
    /// </summary>
    /// <param name="socket">The client end of the loopback pair.</param>
    /// <param name="handlerTask">The task running the channel handler on the server end.</param>
    /// <returns>A task that completes when the close was sent and the handler ended or the grace period elapsed.</returns>
    private static async Task CloseStoppedSessionAsync(WebSocket socket, Task handlerTask)
    {
        try
        {
            using var grace = new CancellationTokenSource(CloseGrace);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "stopped", grace.Token)
                .ConfigureAwait(false);
            await handlerTask.WaitAsync(CloseGrace).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or WebSocketException
                                       or ObjectDisposedException or InvalidOperationException)
        {
            // The channel is already gone; the caller aborts whatever is left.
        }
    }

    /// <summary>
    /// Claims the next command of the emulated worker and reports it: received, then succeeded for a stop or failed for
    /// every action the emulation cannot perform.
    /// </summary>
    /// <param name="session">The authenticated emulated worker.</param>
    /// <param name="channel">The channel the frames are sent on.</param>
    /// <param name="cancellationToken">A token that cancels the exchange.</param>
    /// <returns>
    /// <c>handled</c> is true when a command was claimed and fully reported; <c>stopSucceeded</c> is true when that
    /// command was a stop the control plane accepted as succeeded.
    /// </returns>
    private async Task<(bool handled, bool stopSucceeded)> ClaimAndReportAsync(
        EmulatedSession session,
        SessionChannel channel,
        CancellationToken cancellationToken)
    {
        var deploymentId = session.Identity.DeploymentId;
        var claimAck = await channel.RequestAsync(
                new WorkerChannelFrame
                {
                    Type = EWorkerChannelFrameType.Claim,
                    Id = Guid.NewGuid(),
                    Payload = JsonSerializer.SerializeToElement(
                        new WorkerCommandClaimRequest { DeploymentId = deploymentId },
                        WorkerChannelJson.Options),
                },
                AckTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (claimAck is not
            {
                Outcome: EWorkerChannelOutcome.Accepted, Payload: { ValueKind: JsonValueKind.Object } payload,
            })
        {
            return (false, false);
        }

        WorkerCommandClaimResponse? claim;
        try
        {
            claim = payload.Deserialize<WorkerCommandClaimResponse>(WorkerChannelJson.Options);
        }
        catch (JsonException)
        {
            logger.LogWarning(
                "UAT worker emulation could not read a claimed command of deployment {DeploymentId}",
                deploymentId);
            return (false, false);
        }

        if (claim is null)
            return (false, false);
        var emulated = UatEmulatedCommandReports.IsEmulated(claim.ActionIdentifier);
        logger.LogInformation(
            "UAT worker emulation claimed command {CommandId} ({Action}) of deployment {DeploymentId}",
            claim.CommandId,
            claim.ActionIdentifier,
            deploymentId);
        var received = await ReportAsync(
                channel,
                EWorkerChannelFrameType.ReportReceived,
                claim.CommandId,
                UatEmulatedCommandReports.Received(deploymentId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!received)
            return (false, false);
        var reported = emulated
            ? await ReportAsync(
                    channel,
                    EWorkerChannelFrameType.ReportSucceeded,
                    claim.CommandId,
                    UatEmulatedCommandReports.StoppedSucceeded(
                        deploymentId,
                        claim,
                        NodaSystemClock.Instance.GetCurrentInstant().ToDateTimeOffset()),
                    cancellationToken)
                .ConfigureAwait(false)
            : await ReportAsync(
                    channel,
                    EWorkerChannelFrameType.ReportFailed,
                    claim.CommandId,
                    UatEmulatedCommandReports.Unsupported(deploymentId, claim),
                    cancellationToken)
                .ConfigureAwait(false);
        if (reported)
        {
            logger.LogInformation(
                "UAT worker emulation reported command {CommandId} ({Action}) as {Outcome}",
                claim.CommandId,
                claim.ActionIdentifier,
                emulated ? "succeeded" : UatEmulatedCommandReports.UnsupportedActionCode);
        }

        return (reported, reported && emulated);
    }

    /// <summary>
    /// Sends one command report frame and waits for its acknowledgement.
    /// </summary>
    /// <typeparam name="TReport">The report payload type selected by the frame type.</typeparam>
    /// <param name="channel">The channel the frame is sent on.</param>
    /// <param name="type">The report frame type.</param>
    /// <param name="commandId">The command the report refers to.</param>
    /// <param name="report">The report payload.</param>
    /// <param name="cancellationToken">A token that cancels the exchange.</param>
    /// <returns><see langword="true"/> when the control plane accepted the report.</returns>
    private async Task<bool> ReportAsync<TReport>(
        SessionChannel channel,
        EWorkerChannelFrameType type,
        Guid commandId,
        TReport report,
        CancellationToken cancellationToken)
    {
        var ack = await channel.RequestAsync(
                new WorkerChannelFrame
                {
                    Type = type,
                    Id = Guid.NewGuid(),
                    CommandId = commandId,
                    Payload = JsonSerializer.SerializeToElement(report, WorkerChannelJson.Options),
                },
                AckTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (ack is { Outcome: EWorkerChannelOutcome.Accepted })
            return true;
        logger.LogInformation(
            "UAT worker emulation report {FrameType} of command {CommandId} was {Outcome}",
            type,
            commandId,
            ack?.Outcome?.ToString() ?? "unanswered");
        return false;
    }

    /// <summary>
    /// Builds the heartbeat frame of the emulated worker: process evidence from the worker row, no workloads.
    /// </summary>
    /// <param name="session">The emulated worker session.</param>
    /// <returns>The frame with a fresh request id.</returns>
    private static WorkerChannelFrame HeartbeatFrame(EmulatedSession session)
        => new()
        {
            Type = EWorkerChannelFrameType.Heartbeat,
            Id = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(
                new WorkerHeartbeatRequest
                {
                    DeploymentId = session.Identity.DeploymentId,
                    Stage = EmulatedStage,
                    ProcessId = session.ProcessId,
                    ProcessStartedAt = session.ProcessStartedAt,
                    Workloads = [],
                },
                WorkerChannelJson.Options),
        };

    /// <summary>
    /// Builds the metrics frame with the synthetic CPU and memory series of <see cref="UatMetricHistorySeeder"/> at the
    /// current time and the player count of the session's profile.
    /// </summary>
    /// <param name="session">The emulated worker session.</param>
    /// <returns>The frame with a fresh request id.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The session's player profile is not known.</exception>
    private static WorkerChannelFrame MetricsFrame(EmulatedSession session)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var (cpu, memory, syntheticPlayers) = UatMetricHistorySeeder.SampleAt(now);
        var players = session.Profile switch
        {
            EUatPlayerProfile.Synthetic => syntheticPlayers,
            EUatPlayerProfile.NoPlayers => 0,
            EUatPlayerProfile.FewPlayers => FewPlayersCount,
            _ => throw new ArgumentOutOfRangeException(nameof(session), session.Profile, "Unknown player profile."),
        };
        return new WorkerChannelFrame
        {
            Type = EWorkerChannelFrameType.Metrics,
            Id = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(
                new WorkerMetricsSample
                {
                    DeploymentId = session.Identity.DeploymentId,
                    SampledAt = now.ToDateTimeOffset(),
                    CpuPercent = cpu,
                    MemoryUsedBytes = memory,
                    MemoryTotalBytes = UatMetricHistorySeeder.MemoryTotalBytes,
                    PlayerCount = players,
                    PlayerCapacity = UatMetricHistorySeeder.PlayerCapacity,
                },
                WorkerChannelJson.Options),
        };
    }

    /// <summary>
    /// The client end of one session: sends frames and correlates the acknowledgements and Wake frames the receive loop
    /// reads.
    /// </summary>
    /// <remarks>
    /// Only the session loop sends. The receive loop only reads, so sending and receiving never contend on the socket.
    /// </remarks>
    /// <param name="socket">The client end of the loopback pair.</param>
    private sealed class SessionChannel(WebSocket socket)
    {
        /// <summary>Requests that wait for the acknowledgement carrying their id.</summary>
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<WorkerChannelFrame>> _pending = new();

        /// <summary>Completes when a Wake frame arrived since the last <see cref="TryConsumeWake"/>.</summary>
        private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The task running the receive loop; null until <see cref="StartReceiving"/> ran.</summary>
        private Task? _receiveTask;

        /// <summary>Gets a task that completes when the control plane signalled that a command may be waiting.</summary>
        /// <value>The task of the current, not yet consumed Wake signal.</value>
        public Task WakeTask => Volatile.Read(ref _wake).Task;

        /// <summary>
        /// Starts reading server frames.
        /// </summary>
        /// <param name="cancellationToken">A token that ends reading.</param>
        /// <returns>A task that completes when the server closed the channel or reading failed.</returns>
        public Task StartReceiving(CancellationToken cancellationToken)
            => _receiveTask = ReceiveAsync(cancellationToken);

        /// <summary>
        /// Consumes a pending Wake signal.
        /// </summary>
        /// <returns><see langword="true"/> when a Wake frame arrived since the last call.</returns>
        public bool TryConsumeWake()
        {
            var current = Volatile.Read(ref _wake);
            if (!current.Task.IsCompleted)
                return false;
            Interlocked.CompareExchange(
                ref _wake,
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                current);
            return true;
        }

        /// <summary>
        /// Sends one frame as a text message from the client end.
        /// </summary>
        /// <param name="frame">The frame to send.</param>
        /// <param name="cancellationToken">A token that cancels the send.</param>
        /// <returns>A task that completes once the message was written.</returns>
        public Task SendAsync(WorkerChannelFrame frame, CancellationToken cancellationToken)
            => socket.SendAsync(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame, WorkerChannelJson.Options)),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

        /// <summary>
        /// Sends a request frame and waits for the acknowledgement that echoes its id.
        /// </summary>
        /// <param name="frame">The request frame; it must carry an id.</param>
        /// <param name="timeout">How long to wait for the acknowledgement.</param>
        /// <param name="cancellationToken">A token that cancels the exchange.</param>
        /// <returns>
        /// The acknowledgement, or <see langword="null"/> when none arrived before the channel ended or the timeout.
        /// </returns>
        /// <exception cref="ArgumentException">The frame carries no id.</exception>
        public async Task<WorkerChannelFrame?> RequestAsync(
            WorkerChannelFrame frame,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var id = frame.Id ?? throw new ArgumentException("A request frame needs an id.", nameof(frame));
            var completion = new TaskCompletionSource<WorkerChannelFrame>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = completion;
            try
            {
                await SendAsync(frame, cancellationToken).ConfigureAwait(false);
                using var delay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await Task.WhenAny(
                        completion.Task,
                        _receiveTask ?? Task.CompletedTask,
                        Task.Delay(timeout, delay.Token))
                    .ConfigureAwait(false);
                await delay.CancelAsync().ConfigureAwait(false);
                return completion.Task.IsCompletedSuccessfully ? completion.Task.Result : null;
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        /// <summary>
        /// Reads every server message until the server closes the channel and dispatches acknowledgements and Wake
        /// frames.
        /// </summary>
        /// <param name="cancellationToken">A token that ends reading.</param>
        /// <returns>A task that completes when the server closed the channel or reading failed.</returns>
        private async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveChunkBytes];
            var message = new ArrayBufferWriter<byte>();
            try
            {
                while (socket.State is WebSocketState.Open or WebSocketState.CloseSent)
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType is WebSocketMessageType.Close)
                        return;
                    if (message.WrittenCount + result.Count <= MaxMessageBytes)
                        message.Write(buffer.AsSpan(0, result.Count));
                    if (!result.EndOfMessage)
                        continue;
                    var isComplete = message.WrittenCount <= MaxMessageBytes
                                     && result.MessageType is WebSocketMessageType.Text;
                    var bytes = isComplete ? message.WrittenSpan.ToArray() : null;
                    message.ResetWrittenCount();
                    if (bytes is not null)
                        Dispatch(bytes);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException
                                           or ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Routes one received message: an acknowledgement completes its request, a Wake frame signals the session
        /// loop.
        /// </summary>
        /// <param name="bytes">The UTF-8 text of the message.</param>
        private void Dispatch(byte[] bytes)
        {
            WorkerChannelFrame? frame;
            try
            {
                frame = JsonSerializer.Deserialize<WorkerChannelFrame>(bytes, WorkerChannelJson.Options);
            }
            catch (JsonException)
            {
                return;
            }

            if (frame is null)
                return;
            if (frame.Type is EWorkerChannelFrameType.Wake)
            {
                Volatile.Read(ref _wake).TrySetResult();
            }
            else if (frame is { Type: EWorkerChannelFrameType.Ack, Id: { } id }
                     && _pending.TryGetValue(id, out var completion))
            {
                completion.TrySetResult(frame);
            }
        }
    }

    /// <summary>
    /// The authenticated emulated worker and the evidence its heartbeats report.
    /// </summary>
    /// <param name="Identity">The identity returned by the protocol service.</param>
    /// <param name="Credential">The credential issued for this run; it is never logged.</param>
    /// <param name="ProcessId">The game process id recorded on the worker row, if any.</param>
    /// <param name="ProcessStartedAt">The game process start recorded on the worker row, if any.</param>
    /// <param name="Profile">The player feed the session reports in its metrics frames.</param>
    private sealed record EmulatedSession(
        AuthenticatedWorker Identity,
        string Credential,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAt,
        EUatPlayerProfile Profile);
}
