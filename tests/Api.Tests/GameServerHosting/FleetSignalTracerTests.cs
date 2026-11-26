using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Tracer tests that prove the live fleet signal end to end: a committed lifecycle write travels through the
/// interceptor, the serialized notifier, the publish-time audience source, the hosting authorization service and the
/// update stream service to the sockets of exactly the users who may view the changed server.
/// </summary>
public sealed class FleetSignalTracerTests
{
    /// <summary>
    /// The server identifier used in the pinned wire frame shared with the React tracer test.
    /// </summary>
    private const string PinnedServerId = "3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01";

    /// <summary>
    /// The pinned wire frame, byte for byte identical to the literal in the React tracer test.
    /// </summary>
    private const string PinnedFrame =
        "{\"$id\":\"1\",\"type\":\"X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage\","
        + "\"value\":{\"$id\":\"2\",\"serverId\":\"3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01\"}}";

    /// <summary>
    /// The time a socket that must receive nothing is observed before the absence of a frame is asserted.
    /// </summary>
    private static readonly TimeSpan SettlePeriod = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// A lifecycle job insert committed through an intercepted context reaches the socket of a user who may view the
    /// server as exactly one text frame equal to the pinned frame, and never reaches the socket of a user whose only
    /// view claim is bound to another server.
    /// </summary>
    /// <returns>A task completing after both sockets were verified.</returns>
    [Fact]
    public async Task CommittedLifecycleJobInsert_ReachesOnlySocketOfUserWhoMayViewTheServer()
    {
        await using var harness = await RealLayerHarness.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(harness.SeedContext, withActiveDeployment: true);
        var claims = new ClaimRows(harness.SeedContext);
        var viewer = CreateUser("viewer");
        viewer.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString()));
        var outsider = CreateUser("outsider");
        outsider.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.Server.View, Guid.NewGuid().ToString()));
        harness.SeedContext.Users.AddRange(viewer, outsider);
        await harness.SeedContext.SaveChangesAsync();
        var viewerSocket = harness.RegisterSocket(viewer.PrimaryKey);
        var outsiderSocket = harness.RegisterSocket(outsider.PrimaryKey);

        await harness.CommitJobInsertAsync(seed, sequence: 1);

        var frame = await viewerSocket.NextFrameAsync();
        Assert.Equal(WebSocketMessageType.Text, frame.MessageType);
        Assert.Equal(ExpectedFrame(seed.Server.PrimaryKey), frame.Text);
        await Task.Delay(SettlePeriod);
        Assert.False(viewerSocket.TryTakeFrame(out _), "Exactly one frame is expected for one committed job insert.");
        Assert.False(outsiderSocket.TryTakeFrame(out _), "A user who may not view the server must receive nothing.");
    }

    /// <summary>
    /// With real users, roles and claims, a job insert for the server delivers exactly one pinned frame to every user
    /// the view rule allows through a user claim, a role claim or an administrative claim, and no frame to users who
    /// hold a view claim for another server, a bare view claim or only the creation claim, or who are banned or
    /// deleted.
    /// </summary>
    /// <returns>A task completing after every socket was verified.</returns>
    [Fact]
    public async Task CommittedLifecycleJobInsert_ReachesExactlyTheUsersTheViewRuleAllows()
    {
        await using var harness = await RealLayerHarness.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(harness.SeedContext, withActiveDeployment: true);
        var server = seed.Server.PrimaryKey.ToString();
        var claims = new ClaimRows(harness.SeedContext);

        var directViewer = CreateUser("direct sv:view bound to the server");
        directViewer.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.Server.View, server));
        var roleViewer = CreateUser("sv bound to the server through a role");
        roleViewer.Roles!.Add(
            CreateRole("server operators", await claims.GetAsync(Claims.ResourceBased.Server.All, server)));
        var globalAdministrator = CreateUser("empty adm");
        globalAdministrator.Claims!.Add(await claims.GetAsync(Claims.Administrative.All, string.Empty));
        var roleServerAdministrator = CreateUser("adm:sv through a role");
        roleServerAdministrator.Roles!.Add(
            CreateRole("server administrators", await claims.GetAsync(Claims.Administrative.Server, string.Empty)));
        var otherServerViewer = CreateUser("sv:view bound to another server");
        otherServerViewer.Claims!.Add(
            await claims.GetAsync(Claims.ResourceBased.Server.View, Guid.NewGuid().ToString()));
        var bareViewer = CreateUser("bare sv:view");
        bareViewer.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.Server.View, null));
        var creator = CreateUser("only crt:sv");
        creator.Claims!.Add(await claims.GetAsync(Claims.Creation.Server, null));
        var bannedAdministrator = CreateUser("banned holder of adm");
        bannedAdministrator.IsBanned = true;
        bannedAdministrator.Claims!.Add(await claims.GetAsync(Claims.Administrative.All, string.Empty));
        var deletedAdministrator = CreateUser("deleted holder of adm");
        deletedAdministrator.IsDeleted = true;
        deletedAdministrator.Claims!.Add(await claims.GetAsync(Claims.Administrative.All, string.Empty));

        (User User, bool MayView)[] matrix =
        [
            (directViewer, true),
            (roleViewer, true),
            (globalAdministrator, true),
            (roleServerAdministrator, true),
            (otherServerViewer, false),
            (bareViewer, false),
            (creator, false),
            (bannedAdministrator, false),
            (deletedAdministrator, false),
        ];
        harness.SeedContext.Users.AddRange(matrix.Select(row => row.User));
        await harness.SeedContext.SaveChangesAsync();
        var sockets = matrix
            .Select(row => (row.User.Nickname, row.MayView, Socket: harness.RegisterSocket(row.User.PrimaryKey)))
            .ToArray();

        await harness.CommitJobInsertAsync(seed, sequence: 1);

        foreach (var (name, _, socket) in sockets.Where(row => row.MayView))
            await AssertReceivesPinnedFrameAsync(socket, seed.Server.PrimaryKey, name);
        await Task.Delay(SettlePeriod);
        foreach (var (name, mayView, socket) in sockets)
        {
            Assert.False(
                socket.TryTakeFrame(out _),
                mayView
                    ? $"{name} must receive exactly one frame for one committed job insert."
                    : $"{name} may not view the server and must receive nothing.");
        }
    }

    /// <summary>
    /// After a viewer's claim is removed, the next job insert for the server no longer reaches that user while the
    /// role-based viewer still receives it, because the audience is evaluated at publish time.
    /// </summary>
    /// <returns>A task completing after both deliveries were verified.</returns>
    [Fact]
    public async Task RevokedViewClaim_StopsDeliveryWithTheNextSignal()
    {
        await using var harness = await RealLayerHarness.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(harness.SeedContext, withActiveDeployment: true);
        var claims = new ClaimRows(harness.SeedContext);
        var viewClaim = await claims.GetAsync(Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString());
        var formerViewer = CreateUser("former direct viewer");
        formerViewer.Claims!.Add(viewClaim);
        var roleViewer = CreateUser("role-based viewer");
        roleViewer.Roles!.Add(CreateRole("viewers", viewClaim));
        harness.SeedContext.Users.AddRange(formerViewer, roleViewer);
        await harness.SeedContext.SaveChangesAsync();
        var formerViewerSocket = harness.RegisterSocket(formerViewer.PrimaryKey);
        var roleViewerSocket = harness.RegisterSocket(roleViewer.PrimaryKey);

        await harness.CommitJobInsertAsync(seed, sequence: 1);
        await AssertReceivesPinnedFrameAsync(formerViewerSocket, seed.Server.PrimaryKey, formerViewer.Nickname);
        await AssertReceivesPinnedFrameAsync(roleViewerSocket, seed.Server.PrimaryKey, roleViewer.Nickname);
        await Task.Delay(SettlePeriod);

        formerViewer.Claims.Remove(viewClaim);
        await harness.SeedContext.SaveChangesAsync();
        await harness.CommitJobInsertAsync(seed, sequence: 2);

        await AssertReceivesPinnedFrameAsync(roleViewerSocket, seed.Server.PrimaryKey, roleViewer.Nickname);
        await Task.Delay(SettlePeriod);
        Assert.False(
            formerViewerSocket.TryTakeFrame(out _),
            $"{formerViewer.Nickname} lost the view claim and must not receive the next signal.");
        Assert.False(
            roleViewerSocket.TryTakeFrame(out _),
            $"{roleViewer.Nickname} must receive exactly one frame per committed job insert.");
    }

    /// <summary>
    /// A service collection that mirrors the production registrations resolves the notifier without a construction
    /// cycle under scope and build validation, and a job insert committed through a context created by the context
    /// factory reaches a viewer's socket as the pinned frame.
    /// </summary>
    /// <returns>A task completing after the resolution and the delivery were verified.</returns>
    [Fact]
    public async Task ProductionRegistrationGraph_ResolvesWithoutCycle_AndDeliversThroughFactoryContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUpdateStreamService, UpdateStreamService>();
        services.AddSingleton<FleetChangeNotifier>();
        services.AddSingleton<IFleetChangeNotifier>(
            serviceProvider => serviceProvider.GetRequiredService<FleetChangeNotifier>());
        services.AddHostedService<FleetChangeNotifier>(
            serviceProvider => serviceProvider.GetRequiredService<FleetChangeNotifier>());
        services.AddSingleton<FleetChangeInterceptor>();
        services.AddSingleton<IFleetAudienceSource, FleetAudienceSource>();
        services.AddScoped<IHostingAuthorizationService, HostingAuthorizationService>();
        services.AddDbContextFactory<NodaTimeSqliteApiDbContext>(
            (serviceProvider, options) =>
            {
                options.UseSqlite(connection);
                options.AddInterceptors(serviceProvider.GetRequiredService<FleetChangeInterceptor>());
            });
        services.AddScoped<DatabaseContext>(
            serviceProvider => serviceProvider
                .GetRequiredService<IDbContextFactory<NodaTimeSqliteApiDbContext>>()
                .CreateDbContext());
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes  = true,
                ValidateOnBuild = true,
            });

        FleetChangeNotifier? notifier = null;
        var resolution = Record.Exception(() => notifier = provider.GetRequiredService<FleetChangeNotifier>());
        Assert.Null(resolution);
        Assert.NotNull(notifier);
        var factory = provider.GetRequiredService<IDbContextFactory<NodaTimeSqliteApiDbContext>>();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        Assert.Same(notifier, Assert.Single(hostedServices));

        var updateStream = provider.GetRequiredService<IUpdateStreamService>();
        using var registrationCancellation = new CancellationTokenSource();
        Task? registration = null;
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            await using var seedContext = new NodaTimeSqliteApiDbContext(
                new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(connection).Options);
            await seedContext.Database.EnsureCreatedAsync();
            var seed = await HostingTestData.SeedServerAsync(seedContext, withActiveDeployment: true);
            var claims = new ClaimRows(seedContext);
            var viewer = CreateUser("viewer in the production graph");
            viewer.Claims!.Add(
                await claims.GetAsync(Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString()));
            seedContext.Users.Add(viewer);
            await seedContext.SaveChangesAsync();
            var socket = new RecordingWebSocket();
            var viewerPath = FleetChangeStreamPaths.ForUser(viewer.PrimaryKey);
            registration = updateStream.RegisterAsync(viewerPath, socket, registrationCancellation.Token).AsTask();

            await using (var factoryContext = await factory.CreateDbContextAsync())
                await RealLayerHarness.CommitJobInsertAsync(factoryContext, seed, sequence: 1);

            await AssertReceivesPinnedFrameAsync(socket, seed.Server.PrimaryKey, viewer.Nickname);
            await Task.Delay(SettlePeriod);
            Assert.False(socket.TryTakeFrame(out _), "Exactly one frame is expected for one committed job insert.");
        }
        finally
        {
            await registrationCancellation.CancelAsync();
            if (registration is not null)
                await registration;
            await notifier.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// The fleet subscription is forbidden without a server-section claim and passes the gate for a bare
    /// <c>sv</c> claim; the gate always resolves to the fleet path addressed to the authenticated user.
    /// </summary>
    /// <returns>A task completing after the gate outcomes were verified.</returns>
    [Fact]
    public async Task FleetSubscription_IsGatedByServerSectionClaims()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await using var updateStream = new UpdateStreamService();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var withoutClaim = CreateController(store, updateStream, HostingTestData.Principal(seed.User));
        var withServerClaim = CreateController(
            store,
            updateStream,
            HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.All, string.Empty)));

        Assert.IsType<ForbidResult>(await withoutClaim.SubscribeAsync("game-servers/fleet"));
        Assert.IsType<BadRequestResult>(await withServerClaim.SubscribeAsync("game-servers/fleet"));
        Assert.Equal(
            FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey),
            await withServerClaim.ResolveSubscriptionPathAsync(
                " Game-Servers / FLEET ",
                seed.User.PrimaryKey,
                CancellationToken.None));
        Assert.Null(
            await withServerClaim.ResolveSubscriptionPathAsync(
                "game-servers/fleet/extra",
                seed.User.PrimaryKey,
                CancellationToken.None));
    }

    /// <summary>
    /// Builds the pinned frame for the given server id.
    /// </summary>
    /// <param name="serverId">The id of the server the frame names.</param>
    /// <returns>The pinned frame with the pinned server id replaced by <paramref name="serverId"/>.</returns>
    private static string ExpectedFrame(Guid serverId)
        => PinnedFrame.Replace(PinnedServerId, serverId.ToString(), StringComparison.Ordinal);

    /// <summary>
    /// Waits for the next frame of a socket and asserts that it is the pinned text frame for the server.
    /// </summary>
    /// <param name="socket">The socket that must receive the frame.</param>
    /// <param name="serverId">The id of the server the frame must name.</param>
    /// <param name="userName">The name of the socket's user, used in assertion messages.</param>
    /// <returns>A task completing after the frame was verified.</returns>
    private static async Task AssertReceivesPinnedFrameAsync(RecordingWebSocket socket, Guid serverId, string userName)
    {
        RecordedFrame frame;
        try
        {
            frame = await socket.NextFrameAsync();
        }
        catch (TimeoutException)
        {
            Assert.Fail($"{userName} may view the server but received no frame.");
            throw;
        }

        Assert.True(
            frame.MessageType == WebSocketMessageType.Text,
            $"{userName} received a {frame.MessageType} frame instead of a text frame.");
        var expected = ExpectedFrame(serverId);
        Assert.True(
            string.Equals(expected, frame.Text, StringComparison.Ordinal),
            $"{userName} received '{frame.Text}' instead of the pinned frame '{expected}'.");
    }

    /// <summary>
    /// Creates an unsaved role carrying the given claims.
    /// </summary>
    /// <param name="title">The title of the role.</param>
    /// <param name="claims">The claims the role grants.</param>
    /// <returns>The created role.</returns>
    private static Role CreateRole(string title, params Claim[] claims)
        => new()
        {
            Title       = title,
            Description = "test role",
            Claims      = claims.ToList(),
        };

    /// <summary>
    /// Creates an active, unsaved user with empty claim and role collections.
    /// </summary>
    /// <param name="nickname">The nickname of the user, also used in assertion messages.</param>
    /// <returns>The created user.</returns>
    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord        = new DiscordUser { Username = nickname },
            Steam          = new SteamUser(),
        };

    /// <summary>
    /// Creates an update stream controller whose HTTP context carries the specified principal.
    /// </summary>
    /// <param name="store">The store supplying the database context.</param>
    /// <param name="updateStream">The update stream service the controller registers sockets with.</param>
    /// <param name="principal">The principal of the simulated request.</param>
    /// <returns>The configured controller.</returns>
    private static UpdateStreamController CreateController(
        HostingTestStore store,
        IUpdateStreamService updateStream,
        System.Security.Claims.ClaimsPrincipal principal)
        => new(NullLogger<UpdateStreamController>.Instance, updateStream, store.DbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    /// <summary>
    /// One frame captured by <see cref="RecordingWebSocket"/>.
    /// </summary>
    /// <param name="MessageType">The WebSocket message type of the frame.</param>
    /// <param name="Text">The UTF-8 decoded frame payload.</param>
    private sealed record RecordedFrame(WebSocketMessageType MessageType, string Text);

    /// <summary>
    /// Wires every real layer of the fleet signal over one in-memory SQLite database: the update stream service, the
    /// notifier, a real <see cref="FleetAudienceSource"/> reading through a scoped
    /// <see cref="HostingAuthorizationService"/>, and an intercepted store context for lifecycle writes.
    /// </summary>
    /// <remarks>
    /// The store needs the interceptor, the interceptor needs the notifier, and the notifier needs the audience
    /// source whose scoped context must live on the store's connection. The scoped context factory therefore reads
    /// the connection lazily from a variable that is assigned once the store exists.
    /// </remarks>
    private sealed class RealLayerHarness : IAsyncDisposable
    {
        /// <summary>
        /// Cancels every socket registration when the harness is disposed.
        /// </summary>
        private readonly CancellationTokenSource _registrationCancellation = new();

        /// <summary>
        /// The pending registration tasks, one per registered socket.
        /// </summary>
        private readonly List<Task> _registrations = [];

        /// <summary>
        /// The service provider that supplies the audience source's scoped authorization service and context.
        /// </summary>
        private readonly ServiceProvider _provider;

        /// <summary>
        /// Creates the harness from already wired components.
        /// </summary>
        /// <param name="provider">The provider supplying the scoped authorization service and context.</param>
        /// <param name="updateStream">The real update stream service.</param>
        /// <param name="notifier">The started notifier.</param>
        /// <param name="store">The store whose context carries the interceptor.</param>
        /// <param name="seedContext">The interceptor-free context on the store's connection.</param>
        private RealLayerHarness(
            ServiceProvider provider,
            UpdateStreamService updateStream,
            FleetChangeNotifier notifier,
            HostingTestStore store,
            NodaTimeSqliteApiDbContext seedContext)
        {
            _provider    = provider;
            UpdateStream = updateStream;
            Notifier     = notifier;
            Store        = store;
            SeedContext  = seedContext;
        }

        /// <summary>
        /// Gets the real update stream service the sockets are registered with.
        /// </summary>
        /// <value>The update stream service.</value>
        public UpdateStreamService UpdateStream { get; }

        /// <summary>
        /// Gets the started notifier under test.
        /// </summary>
        /// <value>The notifier.</value>
        public FleetChangeNotifier Notifier { get; }

        /// <summary>
        /// Gets the store whose context carries the fleet change interceptor.
        /// </summary>
        /// <value>The intercepted store.</value>
        public HostingTestStore Store { get; }

        /// <summary>
        /// Gets an interceptor-free context on the store's connection, used for seeding so that only the job inserts
        /// of a test signal.
        /// </summary>
        /// <value>The seeding context.</value>
        public NodaTimeSqliteApiDbContext SeedContext { get; }

        /// <summary>
        /// Wires and starts every real layer.
        /// </summary>
        /// <returns>A task resolving to the started harness.</returns>
        public static async Task<RealLayerHarness> CreateAsync()
        {
            SqliteConnection? connection = null;
            var services = new ServiceCollection();
            services.AddScoped<DatabaseContext>(
                _ => new NodaTimeSqliteApiDbContext(
                    new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
                        .UseSqlite(connection ?? throw new InvalidOperationException("The store does not exist yet."))
                        .Options));
            services.AddScoped<IHostingAuthorizationService, HostingAuthorizationService>();
            var provider = services.BuildServiceProvider();
            var updateStream = new UpdateStreamService();
            var notifier = new FleetChangeNotifier(
                updateStream,
                new FleetAudienceSource(provider.GetRequiredService<IServiceScopeFactory>()),
                NullLogger<FleetChangeNotifier>.Instance)
            {
                // The loop now reads the database before it sends, so the window leaves room for that read.
                CoalesceWindow = TimeSpan.FromMilliseconds(25),
            };
            var interceptor = new FleetChangeInterceptor(notifier, NullLogger<FleetChangeInterceptor>.Instance);
            var store = await HostingTestStore.CreateAsync([interceptor]);
            connection = store.Connection;
            var seedContext = new NodaTimeSqliteApiDbContext(
                new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(store.Connection).Options);
            await notifier.StartAsync(CancellationToken.None);
            return new RealLayerHarness(provider, updateStream, notifier, store, seedContext);
        }

        /// <summary>
        /// Registers a recording socket on the fleet path of one user.
        /// </summary>
        /// <param name="userId">The user whose path the socket is registered on.</param>
        /// <returns>The registered socket.</returns>
        public RecordingWebSocket RegisterSocket(Guid userId)
        {
            var socket = new RecordingWebSocket();
            _registrations.Add(
                UpdateStream
                    .RegisterAsync(FleetChangeStreamPaths.ForUser(userId), socket, _registrationCancellation.Token)
                    .AsTask());
            return socket;
        }

        /// <summary>
        /// Commits one queued start job for the seeded server through the intercepted store context.
        /// </summary>
        /// <param name="seed">The seeded server and deployment the job belongs to.</param>
        /// <param name="sequence">The job sequence, unique per server.</param>
        /// <returns>A task completing once the insert was committed.</returns>
        public Task CommitJobInsertAsync(HostingTestSeed seed, int sequence)
            => CommitJobInsertAsync(Store.DbContext, seed, sequence);

        /// <summary>
        /// Commits one queued start job for the seeded server through the specified context.
        /// </summary>
        /// <param name="context">The intercepted context the job is inserted through.</param>
        /// <param name="seed">The seeded server and deployment the job belongs to.</param>
        /// <param name="sequence">The job sequence, unique per server.</param>
        /// <returns>A task completing once the insert was committed.</returns>
        public static async Task CommitJobInsertAsync(DatabaseContext context, HostingTestSeed seed, int sequence)
        {
            context.LifecycleJobs.Add(
                new LifecycleJob
                {
                    PrimaryKey       = Guid.NewGuid(),
                    GameServerFk     = seed.Server.PrimaryKey,
                    DeploymentFk     = seed.Deployment!.PrimaryKey,
                    Kind             = ELifecycleJobKind.Start,
                    ActionIdentifier = GameServerActions.Start,
                    Status           = ELifecycleJobStatus.Queued,
                    Stage            = "queued",
                    Attempt          = 0,
                    Sequence         = sequence,
                });
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Ends every registration, stops the notifier and releases the contexts, the provider and the stream.
        /// </summary>
        /// <returns>A task completing once every resource was released.</returns>
        public async ValueTask DisposeAsync()
        {
            await _registrationCancellation.CancelAsync();
            await Task.WhenAll(_registrations);
            await Notifier.StopAsync(CancellationToken.None);
            await SeedContext.DisposeAsync();
            await Store.DisposeAsync();
            await _provider.DisposeAsync();
            await UpdateStream.DisposeAsync();
            _registrationCancellation.Dispose();
        }
    }

    /// <summary>
    /// Hands out one shared claim row per identifier and value, reusing seeded catalog rows, because duplicate rows
    /// violate the claim unique indexes.
    /// </summary>
    /// <param name="dbContext">The seeding context the rows are read from and added to.</param>
    private sealed class ClaimRows(DatabaseContext dbContext)
    {
        /// <summary>
        /// The rows handed out so far, keyed by identifier and value (with <c>\0</c> standing for a null value).
        /// </summary>
        private readonly Dictionary<(string Identifier, string Value), Claim> _rows = new();

        /// <summary>
        /// Returns the claim row for an identifier and value, finding a stored row or creating and adding a new one.
        /// </summary>
        /// <param name="identifier">The claim identifier, compared exactly.</param>
        /// <param name="value">The stored claim value, or <see langword="null"/> for a valueless claim.</param>
        /// <returns>A task resolving to the shared claim row.</returns>
        public async Task<Claim> GetAsync(string identifier, string? value)
        {
            var key = (identifier, value ?? "\0");
            if (_rows.TryGetValue(key, out var cached))
                return cached;
            var stored = await dbContext.Claims.SingleOrDefaultAsync(q =>
                q.Identifier == identifier && q.Value == value && q.ValueType == null);
            var claim = stored ?? new Claim
            {
                Identifier  = identifier,
                Value       = value,
                Category    = "test",
                Title       = identifier,
                Description = "test claim",
            };
            if (stored is null)
                dbContext.Claims.Add(claim);
            _rows[key] = claim;
            return claim;
        }
    }

    /// <summary>
    /// An always-open WebSocket double that records every sent frame and blocks receives until cancellation.
    /// </summary>
    private sealed class RecordingWebSocket : WebSocket
    {
        /// <summary>
        /// Holds every frame sent to this socket in send order.
        /// </summary>
        private readonly Channel<RecordedFrame> _frames = Channel.CreateUnbounded<RecordedFrame>();

        /// <inheritdoc />
        public override WebSocketCloseStatus? CloseStatus => null;

        /// <inheritdoc />
        public override string? CloseStatusDescription => null;

        /// <inheritdoc />
        public override WebSocketState State => WebSocketState.Open;

        /// <inheritdoc />
        public override string? SubProtocol => null;

        /// <summary>
        /// Waits for the next recorded frame.
        /// </summary>
        /// <returns>The next frame sent to this socket.</returns>
        /// <exception cref="TimeoutException">No frame arrived within five seconds.</exception>
        public async Task<RecordedFrame> NextFrameAsync()
            => await _frames.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        /// <summary>
        /// Takes an already recorded frame without waiting.
        /// </summary>
        /// <param name="frame">The recorded frame when one was available.</param>
        /// <returns><see langword="true"/> when a recorded frame was available.</returns>
        public bool TryTakeFrame(out RecordedFrame? frame)
        {
            var taken = _frames.Reader.TryRead(out var recorded);
            frame = recorded;
            return taken;
        }

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
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }

        /// <inheritdoc />
        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            _frames.Writer.TryWrite(new RecordedFrame(messageType, Encoding.UTF8.GetString(buffer)));
            return Task.CompletedTask;
        }
    }
}
