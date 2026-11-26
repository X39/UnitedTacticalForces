using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Eventing;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves the fleet subscription gate of <see cref="UpdateStreamController"/>: which principals may subscribe, which
/// requested paths resolve to the fleet stream, that only the path addressed to the authenticated user is registered,
/// and that the existing event subscription behaves exactly as before.
/// </summary>
/// <remarks>
/// The update stream matches subscribers by a raw string prefix, so the gate must never register a client-influenced,
/// longer, padded or differently cased variant of the fleet path, and two users must never share or prefix each
/// other's registered string (research Pitfalls 4 and 7, threats T-62-11, T-62-12, T-62-27 and T-62-28).
/// </remarks>
public sealed class UpdateStreamControllerFleetTests
{
    /// <summary>
    /// A server identifier used as the value of server-bound claims.
    /// </summary>
    private const string BoundServerId = "8d3c0f4e-2b71-4c6a-9e55-1f0a7b9c2d36";

    /// <summary>
    /// The path builder returns the fleet request path, one slash and the user id in the lower-case <c>D</c> format.
    /// </summary>
    [Fact]
    public void ForUser_AppendsLowerCaseDFormatUserIdToFleetRequestPath()
    {
        var userId = Guid.Parse("0A1B2C3D-4E5F-4A6B-8C7D-9E0F1A2B3C4D");

        var path = FleetChangeStreamPaths.ForUser(userId);

        Assert.Equal("game-servers/fleet/0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d", path);
        Assert.Equal("game-servers/fleet/" + userId.ToString("D"), path);
    }

    /// <summary>
    /// Every server-section claim, bare or bound to one server, and an empty global administrator claim resolve the
    /// fleet path to the path addressed to the authenticated user.
    /// </summary>
    /// <param name="claimType">The single claim type the principal holds.</param>
    /// <param name="claimValue">The value of that claim; empty for a bare claim.</param>
    /// <returns>A task completing after the resolved path was verified.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.All, "")]
    [InlineData(Claims.ResourceBased.Server.All, BoundServerId)]
    [InlineData(Claims.ResourceBased.Server.View, "")]
    [InlineData(Claims.ResourceBased.Server.View, BoundServerId)]
    [InlineData(Claims.Creation.Server, "")]
    [InlineData(Claims.Administrative.Server, "")]
    [InlineData(Claims.Administrative.Hosting, "")]
    [InlineData(Claims.Administrative.All, "")]
    public async Task AllowedClaim_ResolvesFleetPathToAuthenticatedUserPath(string claimType, string claimValue)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var controller = CreateController(
            store,
            new RecordingUpdateStreamService(),
            HostingTestData.Principal(seed.User, (claimType, claimValue)));

        var resolved = await controller.ResolveSubscriptionPathAsync(
            "game-servers/fleet",
            seed.User.PrimaryKey,
            CancellationToken.None);

        Assert.Equal(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), resolved);
    }

    /// <summary>
    /// A principal without any server-section claim cannot resolve the fleet path, including holders of the
    /// operate-only server permission and of unrelated sections.
    /// </summary>
    /// <param name="claimType">The single claim type the principal holds, or an empty string for no claim.</param>
    /// <param name="claimValue">The value of that claim.</param>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Theory]
    [InlineData("", "")]
    [InlineData(Claims.ResourceBased.Server.Operate, "")]
    [InlineData(Claims.ResourceBased.Server.Operate, BoundServerId)]
    [InlineData(Claims.ResourceBased.ModPack.All, "")]
    [InlineData(Claims.User.All, "")]
    [InlineData(Claims.ResourceBased.ConfigurationProfile.View, "")]
    [InlineData(Claims.ResourceBased.CostOwner.View, "")]
    public async Task DeniedClaim_DoesNotResolveFleetPath(string claimType, string claimValue)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = claimType.Length == 0
            ? HostingTestData.Principal(seed.User)
            : HostingTestData.Principal(seed.User, (claimType, claimValue));
        var controller = CreateController(store, new RecordingUpdateStreamService(), principal);

        var resolved = await controller.ResolveSubscriptionPathAsync(
            "game-servers/fleet",
            seed.User.PrimaryKey,
            CancellationToken.None);

        Assert.Null(resolved);
    }

    /// <summary>
    /// For an allowed principal, only paths made of exactly the segments <c>game-servers</c> and <c>fleet</c>
    /// resolve, in any casing and with surrounding slashes, and always to the path addressed to the authenticated user.
    /// </summary>
    /// <param name="requestedPath">The path requested by the client.</param>
    /// <param name="resolvesToFleet">Whether the path is expected to resolve to the user's fleet path.</param>
    /// <returns>A task completing after the resolution outcome was verified.</returns>
    [Theory]
    [InlineData("/Game-Servers/FLEET/", true)]
    [InlineData("game-servers/fleet", true)]
    [InlineData("game-servers/fleet/extra", false)]
    [InlineData("game-servers", false)]
    [InlineData("game-servers/other", false)]
    [InlineData("fleet", false)]
    [InlineData("fleet/game-servers", false)]
    [InlineData("", false)]
    public async Task FleetPathMatrix_ResolvesOnlyExactTwoSegmentPath(string requestedPath, bool resolvesToFleet)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var controller = CreateController(
            store,
            new RecordingUpdateStreamService(),
            HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.All, string.Empty)));

        var resolved = await controller.ResolveSubscriptionPathAsync(
            requestedPath,
            seed.User.PrimaryKey,
            CancellationToken.None);

        Assert.Equal(resolvesToFleet ? FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey) : null, resolved);
    }

    /// <summary>
    /// A WebSocket subscription for a differently cased, slash-padded fleet path registers exactly the path addressed
    /// to the authenticated user; a denied principal is forbidden and a non-WebSocket request is rejected after the
    /// gate, and neither registers anything.
    /// </summary>
    /// <returns>A task completing after all three subscription outcomes were verified.</returns>
    [Fact]
    public async Task Subscribe_RegistersOnlyAuthenticatedUserFleetPath()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var allowedPrincipal = HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.View, BoundServerId));

        var acceptedStream = new RecordingUpdateStreamService();
        var acceptedFeature = new StubWebSocketFeature(isWebSocketRequest: true);
        var accepted = CreateController(store, acceptedStream, allowedPrincipal, acceptedFeature);
        var acceptedResult = await accepted.SubscribeAsync("/Game-Servers/FLEET/");

        Assert.IsType<EmptyResult>(acceptedResult);
        Assert.Equal(1, acceptedFeature.AcceptCount);
        Assert.Equal([FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey)], acceptedStream.RegisteredPaths);

        var deniedStream = new RecordingUpdateStreamService();
        var deniedFeature = new StubWebSocketFeature(isWebSocketRequest: true);
        var denied = CreateController(store, deniedStream, HostingTestData.Principal(seed.User), deniedFeature);
        var deniedResult = await denied.SubscribeAsync("/Game-Servers/FLEET/");

        Assert.IsType<ForbidResult>(deniedResult);
        Assert.Equal(0, deniedFeature.AcceptCount);
        Assert.Empty(deniedStream.RegisteredPaths);

        var plainStream = new RecordingUpdateStreamService();
        var plainFeature = new StubWebSocketFeature(isWebSocketRequest: false);
        var plain = CreateController(store, plainStream, allowedPrincipal, plainFeature);
        var plainResult = await plain.SubscribeAsync("game-servers/fleet");

        Assert.IsType<BadRequestResult>(plainResult);
        Assert.Equal(0, plainFeature.AcceptCount);
        Assert.Empty(plainStream.RegisteredPaths);
    }

    /// <summary>
    /// Two different users subscribing through <see cref="UpdateStreamController.SubscribeAsync"/> register two
    /// different strings, each equal to the path built for that user's own id, and neither string is an ordinal prefix
    /// of the other, so the update stream's raw prefix matching cannot cross users (T-62-28).
    /// </summary>
    /// <returns>A task completing after both registrations were compared.</returns>
    [Fact]
    public async Task Subscribe_TwoUsersRegisterDistinctNonPrefixPaths()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var secondUser = CreateUser("second-fleet-viewer");
        store.DbContext.Users.Add(secondUser);
        await store.DbContext.SaveChangesAsync();
        Assert.NotEqual(seed.User.PrimaryKey, secondUser.PrimaryKey);

        var firstPath = await SubscribeAndCaptureAsync(
            store,
            HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.All, string.Empty)),
            "game-servers/fleet");
        var secondPath = await SubscribeAndCaptureAsync(
            store,
            HostingTestData.Principal(secondUser, (Claims.ResourceBased.Server.All, string.Empty)),
            "game-servers/fleet");

        Assert.Equal(FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey), firstPath);
        Assert.Equal(FleetChangeStreamPaths.ForUser(secondUser.PrimaryKey), secondPath);
        Assert.NotEqual(firstPath, secondPath);
        Assert.False(firstPath.StartsWith(secondPath, StringComparison.Ordinal));
        Assert.False(secondPath.StartsWith(firstPath, StringComparison.Ordinal));
    }

    /// <summary>
    /// For an allowed principal, a fleet request carrying a further segment never resolves and never registers a
    /// socket: the caller's own id, another user's id, a dot-dot segment and an escaped dot, each with and without a
    /// trailing slash (T-62-27).
    /// </summary>
    /// <param name="template">
    /// The requested path, where <c>{self}</c> is replaced with the caller's id and <c>{other}</c> with another
    /// user's id at run time.
    /// </param>
    /// <returns>A task completing after the rejection and the absence of any registration were verified.</returns>
    [Theory]
    [InlineData("game-servers/fleet/{self}")]
    [InlineData("game-servers/fleet/{self}/")]
    [InlineData("game-servers/fleet/{other}")]
    [InlineData("game-servers/fleet/{other}/")]
    [InlineData("game-servers/fleet/..")]
    [InlineData("game-servers/fleet/../")]
    [InlineData("game-servers/fleet/%2E")]
    [InlineData("game-servers/fleet/%2E/")]
    public async Task SpoofedFleetPath_DoesNotResolveOrRegister(string template)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var otherUser = CreateUser("spoof-target");
        store.DbContext.Users.Add(otherUser);
        await store.DbContext.SaveChangesAsync();
        var requestedPath = template
            .Replace("{self}", seed.User.PrimaryKey.ToString("D"), StringComparison.Ordinal)
            .Replace("{other}", otherUser.PrimaryKey.ToString("D"), StringComparison.Ordinal);
        var principal = HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.All, string.Empty));

        var resolver = CreateController(store, new RecordingUpdateStreamService(), principal);
        var resolved = await resolver.ResolveSubscriptionPathAsync(
            requestedPath,
            seed.User.PrimaryKey,
            CancellationToken.None);

        var stream = new RecordingUpdateStreamService();
        var feature = new StubWebSocketFeature(isWebSocketRequest: true);
        var subscriber = CreateController(store, stream, principal, feature);
        var result = await subscriber.SubscribeAsync(requestedPath);

        Assert.Null(resolved);
        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, feature.AcceptCount);
        Assert.Empty(stream.RegisteredPaths);
    }

    /// <summary>
    /// The path builder always returns 55 lower-case characters starting with <c>game-servers/fleet/</c>, for the
    /// empty identifier, an identifier parsed from upper-case text and a random identifier.
    /// </summary>
    /// <param name="userIdText">
    /// The text of the user identifier, or an empty string for a freshly generated random identifier.
    /// </param>
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ABCDEF01-2345-4789-ABCD-EF0123456789")]
    [InlineData("{FEDCBA98-7654-4321-BA98-76543210FEDC}")]
    [InlineData("")]
    public void ForUser_HasFixedLengthPrefixAndLowerCase(string userIdText)
    {
        var userId = userIdText.Length == 0 ? Guid.NewGuid() : Guid.Parse(userIdText);

        var path = FleetChangeStreamPaths.ForUser(userId);

        Assert.Equal(55, path.Length);
        Assert.StartsWith("game-servers/fleet/", path, StringComparison.Ordinal);
        Assert.Equal(path.ToLowerInvariant(), path);
    }

    /// <summary>
    /// The registered string does not depend on the casing or the slash padding of the request: the same user
    /// subscribing in four spellings registers one and the same string, the path built for that user's id.
    /// </summary>
    /// <returns>A task completing after all four registrations were compared.</returns>
    [Fact]
    public async Task Subscribe_RegisteredPathIgnoresRequestSpelling()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.View, BoundServerId));
        string[] spellings =
        [
            "game-servers/fleet",
            "/Game-Servers/FLEET/",
            "GAME-SERVERS/fleet//",
            " game-servers / Fleet ",
        ];

        var registered = new List<string>();
        foreach (var spelling in spellings)
            registered.Add(await SubscribeAndCaptureAsync(store, principal, spelling));

        Assert.Equal(spellings.Length, registered.Count);
        Assert.Equal(
            [FleetChangeStreamPaths.ForUser(seed.User.PrimaryKey)],
            registered.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// The event subscription is unchanged: a server-section claim opens no hidden event, an unknown event stays
    /// forbidden even for a global administrator, and a visible event resolves to the requested path unchanged.
    /// </summary>
    /// <returns>A task completing after every event outcome was verified.</returns>
    [Fact]
    public async Task EventSubscription_BehavesAsBefore()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var host = CreateUser("event-host");
        var hiddenEvent = CreateEvent(host, isVisible: false);
        var visibleEvent = CreateEvent(host, isVisible: true);
        store.DbContext.Users.Add(host);
        store.DbContext.Events.AddRange(hiddenEvent, visibleEvent);
        await store.DbContext.SaveChangesAsync();

        var serverHolder = CreateController(
            store,
            new RecordingUpdateStreamService(),
            HostingTestData.Principal(seed.User, (Claims.ResourceBased.Server.All, string.Empty)));
        var administrator = CreateController(
            store,
            new RecordingUpdateStreamService(),
            HostingTestData.Principal(seed.User, (Claims.Administrative.All, string.Empty)));
        var plainUser = CreateController(store, new RecordingUpdateStreamService(), HostingTestData.Principal(seed.User));

        var hiddenPath = $"events/{hiddenEvent.PrimaryKey}";
        var unknownPath = $"events/{Guid.NewGuid()}";
        var visiblePath = $"Events/{visibleEvent.PrimaryKey}/";

        Assert.Null(
            await serverHolder.ResolveSubscriptionPathAsync(hiddenPath, seed.User.PrimaryKey, CancellationToken.None));
        Assert.Null(
            await administrator.ResolveSubscriptionPathAsync(unknownPath, seed.User.PrimaryKey, CancellationToken.None));
        Assert.Equal(
            hiddenPath,
            await administrator.ResolveSubscriptionPathAsync(hiddenPath, seed.User.PrimaryKey, CancellationToken.None));
        Assert.Equal(
            visiblePath,
            await plainUser.ResolveSubscriptionPathAsync(visiblePath, seed.User.PrimaryKey, CancellationToken.None));
    }

    /// <summary>
    /// Creates an update stream controller whose HTTP context carries the principal and, optionally, a WebSocket
    /// feature.
    /// </summary>
    /// <param name="store">The store supplying the database context.</param>
    /// <param name="updateStream">The update stream service the controller registers sockets with.</param>
    /// <param name="principal">The principal of the simulated request.</param>
    /// <param name="webSocketFeature">The WebSocket feature of the request, or <see langword="null"/> for none.</param>
    /// <returns>The configured controller.</returns>
    private static UpdateStreamController CreateController(
        HostingTestStore store,
        IUpdateStreamService updateStream,
        System.Security.Claims.ClaimsPrincipal principal,
        IHttpWebSocketFeature? webSocketFeature = null)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        if (webSocketFeature is not null)
            httpContext.Features.Set(webSocketFeature);
        return new UpdateStreamController(NullLogger<UpdateStreamController>.Instance, updateStream, store.DbContext)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    /// <summary>
    /// Subscribes through <see cref="UpdateStreamController.SubscribeAsync"/> with a WebSocket request and returns the
    /// single string the controller registered with the update stream service.
    /// </summary>
    /// <param name="store">The store supplying the database context.</param>
    /// <param name="principal">The authenticated principal of the simulated request.</param>
    /// <param name="requestedPath">The path the client requests.</param>
    /// <returns>The one registered string.</returns>
    private static async Task<string> SubscribeAndCaptureAsync(
        HostingTestStore store,
        System.Security.Claims.ClaimsPrincipal principal,
        string requestedPath)
    {
        var stream = new RecordingUpdateStreamService();
        var feature = new StubWebSocketFeature(isWebSocketRequest: true);
        var controller = CreateController(store, stream, principal, feature);

        var result = await controller.SubscribeAsync(requestedPath);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal(1, feature.AcceptCount);
        return Assert.Single(stream.RegisteredPaths);
    }

    /// <summary>
    /// Creates a user that can own and host events.
    /// </summary>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>The created, not yet persisted user.</returns>
    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord = new DiscordUser
            {
                Username = nickname,
            },
            Steam = new SteamUser(),
        };

    /// <summary>
    /// Creates an event owned and hosted by the specified user.
    /// </summary>
    /// <param name="host">The owner and host of the event.</param>
    /// <param name="isVisible">Whether the event is visible to every authenticated user.</param>
    /// <returns>The created, not yet persisted event.</returns>
    private static Event CreateEvent(User host, bool isVisible)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return new Event
        {
            PrimaryKey           = Guid.NewGuid(),
            Title                = isVisible ? "Visible operation" : "Hidden operation",
            Description          = "Operation briefing",
            ScheduledForOriginal = now.Plus(Duration.FromDays(7)),
            ScheduledFor         = now.Plus(Duration.FromDays(7)),
            TimeStampCreated     = now,
            IsVisible            = isVisible,
            MinimumAccepted      = 1,
            OwnerFk              = host.PrimaryKey,
            HostedByFk           = host.PrimaryKey,
        };
    }

    /// <summary>
    /// An update stream double that records every registered path and returns immediately.
    /// </summary>
    private sealed class RecordingUpdateStreamService : IUpdateStreamService
    {
        /// <summary>
        /// Holds every registered path in registration order.
        /// </summary>
        private readonly List<string> _registeredPaths = [];

        /// <summary>
        /// Gets every path a socket was registered for.
        /// </summary>
        /// <value>The registered paths in registration order.</value>
        public IReadOnlyList<string> RegisteredPaths => _registeredPaths;

        /// <summary>
        /// Records the registered path and completes immediately instead of waiting for the socket to close.
        /// </summary>
        /// <param name="path">The path the socket is registered for.</param>
        /// <param name="webSocket">The ignored socket.</param>
        /// <param name="cancellationToken">The ignored token.</param>
        /// <returns>A completed task.</returns>
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
        {
            _registeredPaths.Add(path);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Ignores the update, because the gate tests never publish.
        /// </summary>
        /// <typeparam name="T">The package type.</typeparam>
        /// <param name="path">The ignored path.</param>
        /// <param name="package">The ignored package.</param>
        /// <returns>A completed task.</returns>
        public ValueTask SendUpdateAsync<T>(string path, T package)
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A WebSocket feature double that reports a configurable upgrade request and accepts with an idle socket.
    /// </summary>
    private sealed class StubWebSocketFeature : IHttpWebSocketFeature
    {
        /// <summary>
        /// Creates the feature double.
        /// </summary>
        /// <param name="isWebSocketRequest">Whether the simulated request asks for a WebSocket upgrade.</param>
        public StubWebSocketFeature(bool isWebSocketRequest)
            => IsWebSocketRequest = isWebSocketRequest;

        /// <inheritdoc />
        public bool IsWebSocketRequest { get; }

        /// <summary>
        /// Gets how often the controller accepted the upgrade.
        /// </summary>
        /// <value>The number of <see cref="AcceptAsync"/> calls.</value>
        public int AcceptCount { get; private set; }

        /// <summary>
        /// Counts the acceptance and returns an open socket that does nothing.
        /// </summary>
        /// <param name="context">The ignored accept context.</param>
        /// <returns>The idle socket double.</returns>
        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            AcceptCount++;
            return Task.FromResult<WebSocket>(new IdleWebSocket());
        }
    }

    /// <summary>
    /// An always-open WebSocket double whose operations complete without effect.
    /// </summary>
    private sealed class IdleWebSocket : WebSocket
    {
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
        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        /// <inheritdoc />
        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
