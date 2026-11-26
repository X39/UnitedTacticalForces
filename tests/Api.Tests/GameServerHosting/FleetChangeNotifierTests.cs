using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves the delivery half of the live fleet signal: the <see cref="FleetChangeNotifier"/> serializes every send,
/// addresses each send to one recipient of a publish-time audience, coalesces duplicates inside its window, never
/// blocks lifecycle writers, survives failing sends and audience reads, and honors its hosted-service lifecycle.
/// </summary>
/// <remarks>
/// The shared update stream service drops a subscriber whose send throws, and concurrent sends to one socket throw.
/// These tests therefore guard the D-15 invariant that no server-side condition may leave a fleet silently stale.
/// </remarks>
public sealed class FleetChangeNotifierTests
{
    /// <summary>
    /// The upper bound for every condition-based wait; a healthy notifier satisfies each condition far earlier.
    /// </summary>
    private static readonly TimeSpan WaitBound = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The short coalesce window used by tests that do not measure coalescing itself.
    /// </summary>
    private static readonly TimeSpan ShortWindow = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// Two hundred signals written from parallel tasks for five servers never produce overlapping sends, and every
    /// distinct server is delivered at least once.
    /// </summary>
    /// <returns>A task completing after the concurrency maximum and the deliveries were verified.</returns>
    [Fact]
    public async Task ParallelNotify_NeverOverlapsSends_AndDeliversEveryDistinctServer()
    {
        var sender = new FakeUpdateStreamService { SendDelay = TimeSpan.FromMilliseconds(3) };
        var notifier = CreateNotifier(sender, ShortWindow);
        var serverIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            await Task.WhenAll(
                Enumerable.Range(0, 200)
                    .Select(index => Task.Run(() => notifier.Notify(serverIds[index % serverIds.Length]))));

            await WaitUntilAsync(() => serverIds.All(sender.WasDelivered));
            await WaitUntilAsync(() => sender.InFlight == 0);

            Assert.Equal(1, sender.MaximumInFlight);
            Assert.All(serverIds, serverId => Assert.True(sender.WasDelivered(serverId)));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Ten signals for one server inside one coalesce window produce exactly one send, and a signal after the window
    /// produces a second send.
    /// </summary>
    /// <returns>A task completing after both send counts were verified.</returns>
    [Fact]
    public async Task DuplicateNotifyInsideWindow_IsSentOnce_AndLaterNotifyIsSentAgain()
    {
        var sender = new FakeUpdateStreamService();
        var notifier = CreateNotifier(sender, TimeSpan.FromMilliseconds(100));
        var serverId = Guid.NewGuid();
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            for (var index = 0; index < 10; index++)
                notifier.Notify(serverId);

            await WaitUntilAsync(() => sender.AttemptCount >= 1);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.Equal(1, sender.AttemptCount);

            notifier.Notify(serverId);
            await WaitUntilAsync(() => sender.AttemptCount >= 2);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.Equal(2, sender.AttemptCount);
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A send that throws for one server neither prevents the other server of the same batch from being delivered
    /// nor ends the loop for later signals, and it is logged as exactly one warning naming the failed server and the
    /// recipient.
    /// </summary>
    /// <returns>A task completing after deliveries and the warning were verified.</returns>
    [Fact]
    public async Task FailingSend_IsLoggedOnce_AndNeverEndsTheLoop()
    {
        var failingServerId = Guid.NewGuid();
        var siblingServerId = Guid.NewGuid();
        var laterServerId = Guid.NewGuid();
        var sender = new FakeUpdateStreamService { FailingServerId = failingServerId };
        var logger = new CapturingLogger();
        var audience = new FakeAudienceSource();
        var notifier = new FleetChangeNotifier(sender, audience, logger)
        {
            CoalesceWindow = TimeSpan.FromMilliseconds(20),
        };
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            notifier.Notify(failingServerId);
            notifier.Notify(siblingServerId);
            await WaitUntilAsync(() => sender.WasAttempted(failingServerId) && sender.WasDelivered(siblingServerId));

            notifier.Notify(laterServerId);
            await WaitUntilAsync(() => sender.WasDelivered(laterServerId));

            Assert.False(sender.WasDelivered(failingServerId));
            var warnings = logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToArray();
            var warning = Assert.Single(warnings);
            Assert.Contains(failingServerId.ToString(), warning.Message, StringComparison.Ordinal);
            Assert.Contains(audience.DefaultUserId.ToString(), warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// While the sender is blocked on one send, a thousand further signals return immediately, and every one of
    /// them is delivered once the sender is released.
    /// </summary>
    /// <returns>A task completing after the writer latency and the deliveries were verified.</returns>
    [Fact]
    public async Task NotifyWhileSenderIsBlocked_ReturnsImmediately_AndLosesNoServer()
    {
        var sender = new FakeUpdateStreamService();
        sender.BlockSends();
        var notifier = CreateNotifier(sender, ShortWindow);
        var blockingServerId = Guid.NewGuid();
        var queuedServerIds = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToArray();
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            notifier.Notify(blockingServerId);
            await WaitUntilAsync(() => sender.InFlight == 1);

            var stopwatch = Stopwatch.StartNew();
            foreach (var serverId in queuedServerIds)
                notifier.Notify(serverId);
            stopwatch.Stop();

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Notify must not wait for delivery, but 1000 calls took {stopwatch.Elapsed}.");
            Assert.Equal(1, sender.InFlight);

            sender.ReleaseSends();
            await WaitUntilAsync(() => queuedServerIds.All(sender.WasDelivered));
            Assert.True(sender.WasDelivered(blockingServerId));
            Assert.Equal(1, sender.MaximumInFlight);
        }
        finally
        {
            sender.ReleaseSends();
            await notifier.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A signal written before the hosted service started is kept and delivered once the loop starts.
    /// </summary>
    /// <returns>A task completing after the early signal was delivered.</returns>
    [Fact]
    public async Task NotifyBeforeStart_IsDeliveredAfterStart()
    {
        var sender = new FakeUpdateStreamService();
        var notifier = CreateNotifier(sender, ShortWindow);
        var serverId = Guid.NewGuid();

        notifier.Notify(serverId);
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.Equal(0, sender.AttemptCount);

        await notifier.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => sender.WasDelivered(serverId));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Stopping an idle notifier completes promptly, and a signal written after the stop neither throws nor reaches
    /// the sender.
    /// </summary>
    /// <returns>A task completing after the stop latency and the absence of a late send were verified.</returns>
    [Fact]
    public async Task NotifyAfterStop_NeitherThrowsNorDelivers_AndIdleStopIsPrompt()
    {
        var sender = new FakeUpdateStreamService();
        var notifier = CreateNotifier(sender, ShortWindow);
        await notifier.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        await notifier.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        var exception = Record.Exception(() => notifier.Notify(Guid.NewGuid()));
        Assert.Null(exception);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, sender.AttemptCount);
    }

    /// <summary>
    /// Every send targets the path of exactly one recipient, never the bare <c>game-servers/fleet</c> prefix, with a
    /// <see cref="GameServerFleetChangedMessage"/> whose only content is the changed server id.
    /// </summary>
    /// <returns>A task completing after every recorded send was verified.</returns>
    [Fact]
    public async Task EverySend_UsesOneRecipientPath_AndCarriesOnlyTheServerId()
    {
        var sender = new FakeUpdateStreamService();
        var audience = new FakeAudienceSource();
        var notifier = CreateNotifier(sender, ShortWindow, audience);
        var serverIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            foreach (var serverId in serverIds)
                notifier.Notify(serverId);
            await WaitUntilAsync(() => serverIds.All(sender.WasDelivered));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }

        var calls = sender.Calls;
        Assert.Equal(serverIds.Length, calls.Count);
        Assert.All(
            calls,
            call =>
            {
                Assert.Equal(FleetChangeStreamPaths.ForUser(audience.DefaultUserId), call.Path);
                Assert.NotEqual("game-servers/fleet", call.Path);
                Assert.Equal(typeof(GameServerFleetChangedMessage), call.PackageType);
                var message = Assert.IsType<GameServerFleetChangedMessage>(call.Package);
                Assert.Contains(message.ServerId, serverIds);
            });
        var publicProperties = typeof(GameServerFleetChangedMessage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name);
        Assert.Equal([nameof(GameServerFleetChangedMessage.ServerId)], publicProperties);
    }

    /// <summary>
    /// A server whose audience lists two users produces exactly two sends, one on each user's own path, each carrying
    /// that server id; no send path is a prefix of another or equals the bare <c>game-servers/fleet</c> string.
    /// </summary>
    /// <returns>A task completing after the fan-out was verified.</returns>
    [Fact]
    public async Task ServerWithTwoViewers_SendsOncePerViewerOnTheirOwnPaths()
    {
        var sender = new FakeUpdateStreamService();
        var audience = new FakeAudienceSource();
        var serverId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        audience.SetViewers(serverId, firstUserId, secondUserId);
        var notifier = CreateNotifier(sender, ShortWindow, audience);
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            notifier.Notify(serverId);
            await WaitUntilAsync(() => sender.AttemptCount >= 2);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }

        var calls = sender.Calls;
        Assert.Equal(2, calls.Count);
        Assert.Equal(
            new[] { FleetChangeStreamPaths.ForUser(firstUserId), FleetChangeStreamPaths.ForUser(secondUserId) }
                .Order(StringComparer.Ordinal),
            calls.Select(call => call.Path).Order(StringComparer.Ordinal));
        Assert.All(
            calls,
            call =>
            {
                Assert.Equal(serverId, Assert.IsType<GameServerFleetChangedMessage>(call.Package).ServerId);
                Assert.NotEqual("game-servers/fleet", call.Path);
                Assert.DoesNotContain(
                    calls,
                    other => !ReferenceEquals(other, call)
                             && other.Path.StartsWith(call.Path, StringComparison.Ordinal));
            });
    }

    /// <summary>
    /// A server with an empty audience entry and a server missing from the audience result produce no send, while a
    /// viewable server of the same batch is delivered.
    /// </summary>
    /// <returns>A task completing after the absence of sends was verified.</returns>
    [Fact]
    public async Task EmptyOrMissingAudienceEntry_SendsNothingForThatServer()
    {
        var sender = new FakeUpdateStreamService();
        var audience = new FakeAudienceSource();
        var emptyServerId = Guid.NewGuid();
        var missingServerId = Guid.NewGuid();
        var viewableServerId = Guid.NewGuid();
        audience.SetViewers(emptyServerId);
        audience.Omit(missingServerId);
        var notifier = CreateNotifier(sender, TimeSpan.FromMilliseconds(50), audience);
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            notifier.Notify(emptyServerId);
            notifier.Notify(missingServerId);
            notifier.Notify(viewableServerId);
            await WaitUntilAsync(() => sender.WasDelivered(viewableServerId));
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }

        var call = Assert.Single(sender.Calls);
        Assert.Equal(viewableServerId, Assert.IsType<GameServerFleetChangedMessage>(call.Package).ServerId);
        Assert.False(sender.WasAttempted(emptyServerId));
        Assert.False(sender.WasAttempted(missingServerId));
    }

    /// <summary>
    /// Ten notifications for three distinct servers inside one coalesce window produce exactly one audience read
    /// whose requested id set is the three servers.
    /// </summary>
    /// <returns>A task completing after the single audience read was verified.</returns>
    [Fact]
    public async Task OneCoalescedBatch_ReadsTheAudienceOnceForItsDistinctServers()
    {
        var sender = new FakeUpdateStreamService();
        var audience = new FakeAudienceSource();
        var serverIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var notifier = CreateNotifier(sender, TimeSpan.FromMilliseconds(100), audience);
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            for (var index = 0; index < 10; index++)
                notifier.Notify(serverIds[index % serverIds.Length]);

            await WaitUntilAsync(() => serverIds.All(sender.WasDelivered));
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }

        Assert.Equal(1, audience.CallCount);
        var request = Assert.Single(audience.Requests);
        Assert.Equal(serverIds.Order(), request.Order());
        Assert.Equal(serverIds.Length, sender.AttemptCount);
    }

    /// <summary>
    /// When the audience read throws for a batch, that batch sends nothing, exactly one warning and no error-level
    /// entry is logged, the loop keeps running and a later batch is delivered (fail closed, no broadcast fallback).
    /// </summary>
    /// <returns>A task completing after the dropped batch and the later delivery were verified.</returns>
    [Fact]
    public async Task FailingAudienceRead_DropsTheBatchWithOneWarning_AndLaterBatchIsDelivered()
    {
        var sender = new FakeUpdateStreamService();
        var audience = new FakeAudienceSource { FailingCallCount = 1 };
        var logger = new CapturingLogger();
        var droppedServerId = Guid.NewGuid();
        var laterServerId = Guid.NewGuid();
        var notifier = CreateNotifier(sender, ShortWindow, audience, logger);
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            notifier.Notify(droppedServerId);
            await WaitUntilAsync(() => logger.Entries.Any(entry => entry.Level == LogLevel.Warning));
            Assert.Equal(0, sender.AttemptCount);

            notifier.Notify(laterServerId);
            await WaitUntilAsync(() => sender.WasDelivered(laterServerId));
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }

        Assert.Equal(2, audience.CallCount);
        Assert.False(sender.WasAttempted(droppedServerId));
        var call = Assert.Single(sender.Calls);
        Assert.Equal(FleetChangeStreamPaths.ForUser(audience.DefaultUserId), call.Path);
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    /// <summary>
    /// When the send to one recipient throws, the other recipient of the same server and the next server are still
    /// delivered, and exactly one warning names the server and the failed user.
    /// </summary>
    /// <returns>A task completing after the isolated failure was verified.</returns>
    [Fact]
    public async Task FailingRecipientSend_IsIsolated_AndLoggedOnceWithServerAndUser()
    {
        var failingUserId = Guid.NewGuid();
        var siblingUserId = Guid.NewGuid();
        var sender = new FakeUpdateStreamService { FailingPath = FleetChangeStreamPaths.ForUser(failingUserId) };
        var audience = new FakeAudienceSource();
        var logger = new CapturingLogger();
        var sharedServerId = Guid.NewGuid();
        var nextServerId = Guid.NewGuid();
        audience.SetViewers(sharedServerId, failingUserId, siblingUserId);
        var notifier = CreateNotifier(sender, TimeSpan.FromMilliseconds(50), audience, logger);
        await notifier.StartAsync(CancellationToken.None);
        try
        {
            notifier.Notify(sharedServerId);
            notifier.Notify(nextServerId);
            await WaitUntilAsync(
                () => sender.WasDeliveredTo(FleetChangeStreamPaths.ForUser(siblingUserId), sharedServerId)
                      && sender.WasDeliveredTo(FleetChangeStreamPaths.ForUser(audience.DefaultUserId), nextServerId));
            await WaitUntilAsync(() => sender.AttemptCount >= 3);
        }
        finally
        {
            await notifier.StopAsync(CancellationToken.None);
        }

        Assert.False(sender.WasDeliveredTo(FleetChangeStreamPaths.ForUser(failingUserId), sharedServerId));
        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(sharedServerId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(failingUserId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    /// <summary>
    /// Creates a notifier with a capturing logger, the specified coalesce window and an audience double.
    /// </summary>
    /// <param name="sender">The fake update stream service receiving the sends.</param>
    /// <param name="coalesceWindow">The coalesce window of the notifier.</param>
    /// <param name="audience">
    /// The audience double; when <see langword="null"/>, a default double mapping every server to one fixed user.
    /// </param>
    /// <param name="logger">The logger double; when <see langword="null"/>, a fresh capturing logger.</param>
    /// <returns>The created, not yet started notifier.</returns>
    private static FleetChangeNotifier CreateNotifier(
        FakeUpdateStreamService sender,
        TimeSpan coalesceWindow,
        FakeAudienceSource? audience = null,
        CapturingLogger? logger = null)
        => new(sender, audience ?? new FakeAudienceSource(), logger ?? new CapturingLogger())
        {
            CoalesceWindow = coalesceWindow,
        };

    /// <summary>
    /// Polls a condition until it holds or <see cref="WaitBound"/> elapsed.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <returns>A task completing once the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within <see cref="WaitBound"/>.</exception>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > WaitBound)
                throw new TimeoutException($"The awaited condition did not hold within {WaitBound}.");
            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }
    }

    /// <summary>
    /// One send observed by <see cref="FakeUpdateStreamService"/>.
    /// </summary>
    /// <param name="Path">The update-stream path the send targeted.</param>
    /// <param name="PackageType">The static type argument the send was called with.</param>
    /// <param name="Package">The package handed to the send.</param>
    private sealed record RecordedSend(string Path, Type PackageType, object? Package);

    /// <summary>
    /// One log entry captured by <see cref="CapturingLogger"/>.
    /// </summary>
    /// <param name="Level">The level the entry was logged with.</param>
    /// <param name="Message">The formatted message of the entry.</param>
    private sealed record CapturedLogEntry(LogLevel Level, string Message);

    /// <summary>
    /// An update stream double that records sends, tracks how many run at once, and can delay, fail or block sends.
    /// </summary>
    private sealed class FakeUpdateStreamService : IUpdateStreamService
    {
        /// <summary>
        /// Guards <see cref="_calls"/>.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// Every send in the order it started.
        /// </summary>
        private readonly List<RecordedSend> _calls = [];

        /// <summary>
        /// The server ids whose send was attempted, successfully or not.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, byte> _attempted = new();

        /// <summary>
        /// The server ids whose send completed without throwing.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, byte> _delivered = new();

        /// <summary>
        /// The path and server id pairs whose send completed without throwing.
        /// </summary>
        private readonly ConcurrentDictionary<(string Path, Guid ServerId), byte> _deliveredTo = new();

        /// <summary>
        /// The number of sends currently running.
        /// </summary>
        private int _inFlight;

        /// <summary>
        /// The highest number of sends observed running at once.
        /// </summary>
        private int _maximumInFlight;

        /// <summary>
        /// The number of completed send attempts.
        /// </summary>
        private int _attemptCount;

        /// <summary>
        /// Completes when blocked sends may continue; <see langword="null"/> while sends are not blocked.
        /// </summary>
        private TaskCompletionSource? _release;

        /// <summary>
        /// Gets the time every send waits before it completes.
        /// </summary>
        /// <value>The artificial send duration; zero by default.</value>
        public TimeSpan SendDelay { get; init; }

        /// <summary>
        /// Gets the server id whose send throws.
        /// </summary>
        /// <value>The failing server id, or <see langword="null"/> when no send fails.</value>
        public Guid? FailingServerId { get; init; }

        /// <summary>
        /// Gets the recipient path whose sends throw.
        /// </summary>
        /// <value>The failing path, or <see langword="null"/> when no path fails.</value>
        public string? FailingPath { get; init; }

        /// <summary>
        /// Gets the number of sends currently running.
        /// </summary>
        /// <value>The in-flight counter.</value>
        public int InFlight => Volatile.Read(ref _inFlight);

        /// <summary>
        /// Gets the highest number of sends observed running at once.
        /// </summary>
        /// <value>One for a correctly serialized notifier.</value>
        public int MaximumInFlight => Volatile.Read(ref _maximumInFlight);

        /// <summary>
        /// Gets the number of completed send attempts, including failed ones.
        /// </summary>
        /// <value>The completed attempt counter.</value>
        public int AttemptCount => Volatile.Read(ref _attemptCount);

        /// <summary>
        /// Gets a snapshot of every recorded send.
        /// </summary>
        /// <value>The sends in start order.</value>
        public IReadOnlyList<RecordedSend> Calls
        {
            get
            {
                lock (_lock)
                    return _calls.ToArray();
            }
        }

        /// <summary>
        /// Makes every subsequent send wait until <see cref="ReleaseSends"/> is called.
        /// </summary>
        public void BlockSends()
            => _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Lets every blocked and future send continue.
        /// </summary>
        public void ReleaseSends()
            => _release?.TrySetResult();

        /// <summary>
        /// Returns whether a send for the server completed without throwing.
        /// </summary>
        /// <param name="serverId">The server id to check.</param>
        /// <returns><see langword="true"/> when the server was delivered at least once.</returns>
        public bool WasDelivered(Guid serverId)
            => _delivered.ContainsKey(serverId);

        /// <summary>
        /// Returns whether a send for the server to the specified recipient path completed without throwing.
        /// </summary>
        /// <param name="path">The recipient path to check.</param>
        /// <param name="serverId">The server id to check.</param>
        /// <returns><see langword="true"/> when the server was delivered to the path at least once.</returns>
        public bool WasDeliveredTo(string path, Guid serverId)
            => _deliveredTo.ContainsKey((path, serverId));

        /// <summary>
        /// Returns whether a send for the server was attempted and finished, successfully or not.
        /// </summary>
        /// <param name="serverId">The server id to check.</param>
        /// <returns><see langword="true"/> when a send attempt for the server completed.</returns>
        public bool WasAttempted(Guid serverId)
            => _attempted.ContainsKey(serverId);

        /// <summary>
        /// Always throws, because the notifier never registers sockets.
        /// </summary>
        /// <param name="path">The ignored path.</param>
        /// <param name="webSocket">The ignored socket.</param>
        /// <param name="cancellationToken">The ignored token.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => throw new NotSupportedException("The fleet notifier must never register sockets.");

        /// <summary>
        /// Records the send, tracks concurrency, and applies the configured block, delay and failure.
        /// </summary>
        /// <typeparam name="T">The static package type chosen by the caller.</typeparam>
        /// <param name="path">The update-stream path the send targets.</param>
        /// <param name="package">The package to send.</param>
        /// <returns>A task completing once the simulated send finished.</returns>
        /// <exception cref="InvalidOperationException">
        /// The package names <see cref="FailingServerId"/> or the path equals <see cref="FailingPath"/>.
        /// </exception>
        public async ValueTask SendUpdateAsync<T>(string path, T package)
        {
            var current = Interlocked.Increment(ref _inFlight);
            UpdateMaximum(current);
            var serverId = (package as GameServerFleetChangedMessage)?.ServerId ?? Guid.Empty;
            try
            {
                lock (_lock)
                    _calls.Add(new RecordedSend(path, typeof(T), package));

                if (_release is { } release)
                    await release.Task.ConfigureAwait(false);
                if (SendDelay > TimeSpan.Zero)
                    await Task.Delay(SendDelay).ConfigureAwait(false);
                if (serverId == FailingServerId || path == FailingPath)
                    throw new InvalidOperationException("Simulated delivery failure.");

                _delivered.TryAdd(serverId, 0);
                _deliveredTo.TryAdd((path, serverId), 0);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
                _attempted.TryAdd(serverId, 0);
                Interlocked.Increment(ref _attemptCount);
            }
        }

        /// <summary>
        /// Raises <see cref="_maximumInFlight"/> to the observed value when it is higher.
        /// </summary>
        /// <param name="observed">The in-flight count observed by one send.</param>
        private void UpdateMaximum(int observed)
        {
            int snapshot;
            do
            {
                snapshot = Volatile.Read(ref _maximumInFlight);
                if (observed <= snapshot)
                    return;
            }
            while (Interlocked.CompareExchange(ref _maximumInFlight, observed, snapshot) != snapshot);
        }
    }

    /// <summary>
    /// An audience double that maps each server to configured user ids, counts its calls, records the requested ids
    /// and can be told to throw.
    /// </summary>
    private sealed class FakeAudienceSource : IFleetAudienceSource
    {
        /// <summary>
        /// Guards <see cref="_viewers"/>, <see cref="_omitted"/> and <see cref="_requests"/>.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// The configured audience entries; servers not listed map to <see cref="DefaultUserId"/>.
        /// </summary>
        private readonly Dictionary<Guid, Guid[]> _viewers = new();

        /// <summary>
        /// The servers for which the returned dictionary contains no entry at all.
        /// </summary>
        private readonly HashSet<Guid> _omitted = [];

        /// <summary>
        /// The requested server ids of every call, in call order.
        /// </summary>
        private readonly List<Guid[]> _requests = [];

        /// <summary>
        /// The number of calls made so far.
        /// </summary>
        private int _callCount;

        /// <summary>
        /// Gets the user id every server without a configured entry maps to.
        /// </summary>
        /// <value>A fixed identifier generated per double.</value>
        public Guid DefaultUserId { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the number of leading calls that throw instead of returning an audience.
        /// </summary>
        /// <value>Zero by default; one makes only the first call throw.</value>
        public int FailingCallCount { get; init; }

        /// <summary>
        /// Gets the number of calls made so far, including failed ones.
        /// </summary>
        /// <value>The call counter.</value>
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// Gets a snapshot of the server ids requested by every call.
        /// </summary>
        /// <value>One array per call, in call order.</value>
        public IReadOnlyList<Guid[]> Requests
        {
            get
            {
                lock (_lock)
                    return _requests.ToArray();
            }
        }

        /// <summary>
        /// Configures the users returned for one server; passing no user ids configures an empty entry.
        /// </summary>
        /// <param name="serverId">The server whose audience is configured.</param>
        /// <param name="userIds">The users who may view the server.</param>
        public void SetViewers(Guid serverId, params Guid[] userIds)
        {
            lock (_lock)
                _viewers[serverId] = userIds;
        }

        /// <summary>
        /// Makes the returned dictionary contain no entry for one server.
        /// </summary>
        /// <param name="serverId">The server to leave out of every result.</param>
        public void Omit(Guid serverId)
        {
            lock (_lock)
                _omitted.Add(serverId);
        }

        /// <summary>
        /// Records the call, throws for the configured leading calls and otherwise returns the configured audience.
        /// </summary>
        /// <param name="gameServerIds">The requested server ids.</param>
        /// <param name="cancellationToken">The ignored token.</param>
        /// <returns>A task resolving to the configured audience of the requested servers.</returns>
        /// <exception cref="InvalidOperationException">The call is one of the configured failing calls.</exception>
        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetViewersAsync(
            IReadOnlyCollection<Guid> gameServerIds,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            lock (_lock)
            {
                _requests.Add(gameServerIds.ToArray());
                if (call <= FailingCallCount)
                    throw new InvalidOperationException("Simulated audience failure.");

                var result = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
                foreach (var serverId in gameServerIds)
                {
                    if (_omitted.Contains(serverId))
                        continue;
                    result[serverId] = _viewers.TryGetValue(serverId, out var configured)
                        ? configured
                        : [DefaultUserId];
                }

                return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>(result);
            }
        }
    }

    /// <summary>
    /// A logger double that records the level and the formatted message of every entry.
    /// </summary>
    private sealed class CapturingLogger : ILogger<FleetChangeNotifier>
    {
        /// <summary>
        /// Holds every captured entry in log order.
        /// </summary>
        private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

        /// <summary>
        /// Gets a snapshot of every captured entry.
        /// </summary>
        /// <value>The entries in log order.</value>
        public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToArray();

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
            => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _entries.Enqueue(new CapturedLogEntry(logLevel, formatter(state, exception)));
    }
}
