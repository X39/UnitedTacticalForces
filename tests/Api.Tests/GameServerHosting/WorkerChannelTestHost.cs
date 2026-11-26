using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Hosts the real worker protocol service and the worker channel frame routes over a
/// <see cref="HostingTestStore"/> so route tests exercise the unchanged service against a real EF context.
/// </summary>
/// <remarks>
/// The host is internal because it exposes the internal <see cref="HostingTestStore"/>. Later channel tests extend
/// the default registrations through the <c>configure</c> hook of <see cref="CreateAsync"/>.
/// </remarks>
internal sealed class WorkerChannelTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly bool _ownsStore;
    private (AuthenticatedWorker Worker, string Credential, Guid CommandId)? _seeded;

    private WorkerChannelTestHost(ServiceProvider serviceProvider, HostingTestStore store, bool ownsStore)
    {
        _serviceProvider = serviceProvider;
        Store = store;
        _ownsStore = ownsStore;
    }

    /// <summary>
    /// Gets the service provider holding the protocol service, its collaborators and the frame routes.
    /// </summary>
    /// <value>The provider backing the host; resolve scoped services through a scope.</value>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Gets the registry that tracks the live channel connection of each worker.
    /// </summary>
    /// <value>The singleton registry the handler registers every connection in; tests and later plans push through it.</value>
    public IWorkerChannelRegistry Registry => _serviceProvider.GetRequiredService<IWorkerChannelRegistry>();

    /// <summary>
    /// Gets the store whose database context the hosted services share.
    /// </summary>
    /// <value>The store passed to <see cref="CreateAsync"/> or the one the host created.</value>
    public HostingTestStore Store { get; }

    /// <summary>
    /// Creates a host over a store with the protocol service and every channel route registered.
    /// </summary>
    /// <param name="store">The store to host over; a fresh store owned by the host is created when null.</param>
    /// <param name="timeProvider">An optional time provider registered for services that depend on it.</param>
    /// <param name="loggerProvider">An optional logger provider added to the logging pipeline.</param>
    /// <param name="configure">An optional hook run after the default registrations to add or replace services.</param>
    /// <param name="configureOptions">An optional hook that adjusts the worker options, such as the channel limits.</param>
    /// <returns>The created host; dispose it to release the provider and an owned store.</returns>
    public static async Task<WorkerChannelTestHost> CreateAsync(
        HostingTestStore? store = null,
        TimeProvider? timeProvider = null,
        ILoggerProvider? loggerProvider = null,
        Action<IServiceCollection>? configure = null,
        Action<LifecycleWorkerOptions>? configureOptions = null)
    {
        var ownsStore = store is null;
        store ??= await HostingTestStore.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseContext>(store.DbContext);
        var workerOptions = CreateWorkerOptions();
        configureOptions?.Invoke(workerOptions);
        services.AddSingleton(Options.Create(workerOptions));
        services.AddSingleton<IWorkerCredentialService, WorkerCredentialService>();
        services.AddScoped<IHostingAuditWriter, HostingAuditWriter>();
        services.AddSingleton<IGameServerSecretService, TestGameServerSecretService>();
        services.AddScoped<IWorkerProtocolService, WorkerProtocolService>();
        services.AddScoped<IWorkerChannelFrameRoute, HeartbeatFrameRoute>();
        services.AddScoped<IWorkerChannelFrameRoute, ClaimFrameRoute>();
        services.AddScoped<IWorkerChannelFrameRoute, CommandReportFrameRoute>();
        services.AddSingleton<IWorkerChannelRegistry, WorkerChannelRegistry>();
        services.AddSingleton<IWorkerChannelHandler, WorkerChannelHandler>();
        services.AddLogging(builder =>
        {
            if (loggerProvider is not null)
                builder.AddProvider(loggerProvider);
        });
        if (timeProvider is not null)
            services.AddSingleton(timeProvider);
        configure?.Invoke(services);
        return new WorkerChannelTestHost(services.BuildServiceProvider(), store, ownsStore);
    }

    /// <summary>
    /// Seeds a server with a Running deployment, registers its primary worker through the real protocol service and
    /// queues one command.
    /// </summary>
    /// <returns>
    /// The authenticated worker identity built from the seeded row, the registered worker credential and the id of the
    /// seeded Queued command.
    /// </returns>
    public async Task<(AuthenticatedWorker Worker, string Credential, Guid CommandId)> SeedRegisteredWorkerAsync()
    {
        var dbContext = Store.DbContext;
        var hosting = await HostingTestData.SeedServerAsync(
            dbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Running);
        var deployment = hosting.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            Identity = "pending",
            Status = EWorkerStatus.Provisioning,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = hosting.Server.PrimaryKey,
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = hosting.User.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = "start",
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "waiting-for-worker",
            Progress = 20,
            Attempt = 1,
            MaxAttempts = 3,
            LeaseOwner = "control-plane-test",
            LeaseExpires = now + Duration.FromMinutes(1),
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStarted = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = job,
            LifecycleJobFk = job.PrimaryKey,
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            Worker = worker,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            ActionIdentifier = job.ActionIdentifier,
            Payload = "{\"test\":true}",
            Status = EWorkerCommandStatus.Queued,
            Stage = "queued",
            TimeStampCreated = now,
        };
        var bootstrap = _serviceProvider.GetRequiredService<IWorkerCredentialService>()
            .IssueBootstrapCredential(worker);
        dbContext.AddRange(worker, job, command);
        await dbContext.SaveChangesAsync();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var registration = await scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>().RegisterAsync(
            worker.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = deployment.PrimaryKey,
                Identity = "channel-test-worker",
                ProtocolVersion = 2,
                SupportedGameIdentifiers = [HostingTestData.GameIdentifier],
            },
            bootstrap.BootstrapCredential);
        var authenticated = new AuthenticatedWorker(
            worker.PrimaryKey,
            deployment.PrimaryKey,
            hosting.Server.PrimaryKey,
            worker.ServiceIdentifier,
            worker.ServiceOrdinal);
        _seeded = (authenticated, registration.WorkerCredential, command.PrimaryKey);
        return _seeded.Value;
    }

    /// <summary>
    /// Adopts the worker another host over the same store seeded, so a fresh host can serve it after a simulated API
    /// restart.
    /// </summary>
    /// <param name="seeded">The tuple returned by <see cref="SeedRegisteredWorkerAsync"/> on the earlier host.</param>
    public void AdoptSeededWorker((AuthenticatedWorker Worker, string Credential, Guid CommandId) seeded)
        => _seeded = seeded;

    /// <summary>
    /// Authenticates the seeded worker through the real protocol service, starts the channel handler on the server end
    /// of a loopback socket pair and returns the worker end.
    /// </summary>
    /// <param name="credential">
    /// The credential to authenticate with; the credential returned by <see cref="SeedRegisteredWorkerAsync"/> when
    /// null.
    /// </param>
    /// <returns>The connected client; dispose it to end the channel and release the socket pair.</returns>
    /// <exception cref="InvalidOperationException"><see cref="SeedRegisteredWorkerAsync"/> was not called.</exception>
    /// <exception cref="WorkerProtocolAuthenticationException">The credential was rejected.</exception>
    public async Task<WorkerChannelTestClient> ConnectAsync(string? credential = null)
    {
        var (worker, seededCredential, _) = _seeded
            ?? throw new InvalidOperationException("Seed a registered worker before connecting a channel.");
        var heldCredential = credential ?? seededCredential;
        AuthenticatedWorker identity;
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            identity = await scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>()
                .AuthenticateAsync(worker.WorkerId, worker.DeploymentId, heldCredential);
        }

        var pair = await LoopbackWebSocketPair.CreateAsync();
        var cancellation = new CancellationTokenSource();
        var handler = _serviceProvider.GetRequiredService<IWorkerChannelHandler>();
        var serverTask = Task.Run(
            () => handler.RunAsync(identity, heldCredential, pair.Server, cancellation.Token),
            CancellationToken.None);
        return new WorkerChannelTestClient(pair, cancellation, serverTask);
    }

    /// <summary>
    /// Executes one request through the route that handles its frame type, in a fresh dependency injection scope.
    /// </summary>
    /// <param name="request">The frame with its authenticated worker and held credential.</param>
    /// <returns>The route result.</returns>
    /// <exception cref="InvalidOperationException">No registered route handles the frame type.</exception>
    public async Task<WorkerChannelRouteResult> RouteAsync(WorkerChannelRequest request)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var route = scope.ServiceProvider.GetServices<IWorkerChannelFrameRoute>()
            .FirstOrDefault(q => q.FrameTypes.Contains(request.Frame.Type))
            ?? throw new InvalidOperationException(
                $"No worker channel route handles frame type {request.Frame.Type}.");
        return await route.HandleAsync(request, CancellationToken.None);
    }

    /// <summary>
    /// Disposes the service provider and, when the host created it, the store.
    /// </summary>
    /// <returns>A task completing once the owned resources were released.</returns>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        if (_ownsStore)
            await Store.DisposeAsync();
    }

    /// <summary>
    /// The worker end of a channel started by <see cref="ConnectAsync"/>.
    /// </summary>
    public sealed class WorkerChannelTestClient : IAsyncDisposable
    {
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);
        private readonly LoopbackWebSocketPair _pair;
        private readonly CancellationTokenSource _cancellation;
        private int _disposed;

        internal WorkerChannelTestClient(
            LoopbackWebSocketPair pair,
            CancellationTokenSource cancellation,
            Task serverTask)
        {
            _pair = pair;
            _cancellation = cancellation;
            ServerTask = serverTask;
        }

        /// <summary>Gets the task running the channel handler on the server end.</summary>
        /// <value>A task that completes when the handler returned.</value>
        public Task ServerTask { get; }

        /// <summary>Gets the underlying worker-end socket.</summary>
        /// <value>The client-role WebSocket.</value>
        public WebSocket Socket => _pair.Client;

        /// <summary>Sends one frame as a text message.</summary>
        /// <param name="frame">The frame to send.</param>
        /// <returns>A task completing once the message was written.</returns>
        public Task SendAsync(WorkerChannelFrame frame)
            => SendTextAsync(JsonSerializer.Serialize(frame, WorkerChannelJson.Options));

        /// <summary>Sends raw text as one text message, for malformed and oversized frames.</summary>
        /// <param name="text">The message text.</param>
        /// <returns>A task completing once the message was written.</returns>
        public Task SendTextAsync(string text)
            => _pair.Client.SendAsync(
                Encoding.UTF8.GetBytes(text),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

        /// <summary>Receives the next message and deserializes it as a frame.</summary>
        /// <returns>The received frame.</returns>
        /// <exception cref="InvalidOperationException">The server closed the socket instead of sending a frame.</exception>
        public async Task<WorkerChannelFrame> ReceiveAsync()
        {
            var (isClose, closeStatus, text) = await ReceiveMessageAsync();
            if (isClose)
                throw new InvalidOperationException($"The server closed the channel with status {closeStatus}.");
            return JsonSerializer.Deserialize<WorkerChannelFrame>(text, WorkerChannelJson.Options)
                   ?? throw new InvalidOperationException("The server sent a null frame.");
        }

        /// <summary>Receives the next message and requires it to be a close frame.</summary>
        /// <returns>The close status the server sent.</returns>
        /// <exception cref="InvalidOperationException">The server sent a frame instead of closing.</exception>
        public async Task<WebSocketCloseStatus?> ReceiveCloseAsync()
        {
            var (isClose, closeStatus, text) = await ReceiveMessageAsync();
            if (!isClose)
                throw new InvalidOperationException($"Expected a close but received a frame: {text}");
            return closeStatus;
        }

        /// <summary>
        /// Sends a request frame and waits for the ack carrying the same id, skipping Wake frames in between.
        /// </summary>
        /// <param name="frame">The request frame; it must carry an id.</param>
        /// <returns>The ack frame.</returns>
        public async Task<WorkerChannelFrame> RequestAsync(WorkerChannelFrame frame)
        {
            await SendAsync(frame);
            while (true)
            {
                var received = await ReceiveAsync();
                if (received is { Type: EWorkerChannelFrameType.Ack } && received.Id == frame.Id)
                    return received;
            }
        }

        /// <summary>Cancels the handler, waits briefly for it to end and releases the socket pair; repeated calls are no-ops.</summary>
        /// <returns>A task completing once the client was released.</returns>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            await _cancellation.CancelAsync();
            try
            {
                await ServerTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            await _pair.DisposeAsync();
            _cancellation.Dispose();
        }

        private async Task<(bool IsClose, WebSocketCloseStatus? CloseStatus, string Text)> ReceiveMessageAsync()
        {
            using var timeout = new CancellationTokenSource(ReceiveTimeout);
            var buffer = new byte[4096];
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _pair.Client.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType is WebSocketMessageType.Close)
                    return (true, result.CloseStatus, string.Empty);
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
            return (false, null, Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    private static LifecycleWorkerOptions CreateWorkerOptions() => new()
    {
        HeartbeatInterval = TimeSpan.FromSeconds(10),
        JobLease = TimeSpan.FromSeconds(45),
        BootstrapCredentialLifetime = TimeSpan.FromMinutes(10),
        RegistrationGrace = TimeSpan.FromMinutes(2),
        WorkerCredentialLifetime = TimeSpan.FromHours(1),
    };
}
