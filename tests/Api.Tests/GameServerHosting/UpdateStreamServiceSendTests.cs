using System.Net.WebSockets;
using System.Text;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// T-66-16 proof that the update stream serializes sends per socket, so several producers writing to the same user
/// path can no longer make the second send throw and drop the subscriber, while failing sockets are still removed and
/// different sockets never wait for each other.
/// </summary>
public sealed class UpdateStreamServiceSendTests
{
    /// <summary>
    /// Twenty simultaneous sends to one path all arrive on a socket that rejects overlapping sends, and the socket
    /// stays registered for a further send.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task ConcurrentSends_ToOneSocket_AllArriveAndKeepSubscriber()
    {
        await using var service = new UpdateStreamService();
        var socket = new TestWebSocket { SendDelay = TimeSpan.FromMilliseconds(5) };
        using var cancellation = new CancellationTokenSource();
        var registration = service.RegisterAsync("game-servers/fleet/user", socket, cancellation.Token).AsTask();

        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => Task.Run(() => service.SendUpdateAsync("game-servers/fleet/user", new TestMessage($"m{i}")).AsTask())));
        await service.SendUpdateAsync("game-servers/fleet/user", new TestMessage("after"));

        Assert.Equal(21, socket.Frames.Count);
        Assert.Contains(socket.Frames, frame => frame.Contains("\"after\"", StringComparison.Ordinal));
        Assert.Equal(0, socket.OverlapCount);
        socket.Close();
        await registration;
    }

    /// <summary>
    /// A socket whose send throws for another reason is removed and receives nothing further.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task SocketWhoseSendFails_IsRemoved()
    {
        await using var service = new UpdateStreamService();
        var socket = new TestWebSocket { SendFailure = new IOException("broken pipe") };
        using var cancellation = new CancellationTokenSource();
        var registration = service.RegisterAsync("path", socket, cancellation.Token).AsTask();

        await service.SendUpdateAsync("path", new TestMessage("first"));
        await service.SendUpdateAsync("path", new TestMessage("second"));

        Assert.Equal(1, socket.SendAttempts);
        socket.Close();
        await registration;
    }

    /// <summary>
    /// A socket blocked inside a send does not delay a send to another socket.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task BlockedSocket_DoesNotDelayOtherSocket()
    {
        await using var service = new UpdateStreamService();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TestWebSocket { SendGate = gate.Task };
        var free = new TestWebSocket();
        using var cancellation = new CancellationTokenSource();
        var blockedRegistration = service.RegisterAsync("blocked/path", blocked, cancellation.Token).AsTask();
        var freeRegistration = service.RegisterAsync("free/path", free, cancellation.Token).AsTask();

        var blockedSend = service.SendUpdateAsync("blocked/path", new TestMessage("blocked")).AsTask();
        await WaitUntilAsync(() => blocked.SendAttempts == 1);
        var freeSend = service.SendUpdateAsync("free/path", new TestMessage("free")).AsTask();
        var completed = await Task.WhenAny(freeSend, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(freeSend, completed);
        Assert.Single(free.Frames);
        Assert.False(blockedSend.IsCompleted);
        gate.SetResult();
        await blockedSend;
        Assert.Single(blocked.Frames);
        blocked.Close();
        free.Close();
        await Task.WhenAll(blockedRegistration, freeRegistration);
    }

    /// <summary>
    /// Polls a condition until it holds, failing the test after ten seconds.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <returns>A task completing once the condition holds.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not become true in time.");
            await Task.Delay(5);
        }
    }

    /// <summary>
    /// The message type serialized by the tests.
    /// </summary>
    /// <param name="Text">The text carried by the message.</param>
    private sealed record TestMessage(string Text);

    /// <summary>
    /// A WebSocket double that records frames, rejects overlapping sends like a real socket and ends its receive loop
    /// when the test closes it.
    /// </summary>
    private sealed class TestWebSocket : WebSocket
    {
        /// <summary>
        /// Completed by <see cref="Close"/> to end the receive loop.
        /// </summary>
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// The number of sends currently in flight.
        /// </summary>
        private int _inFlight;

        /// <summary>
        /// The number of overlapping sends observed.
        /// </summary>
        private int _overlaps;

        /// <summary>
        /// The number of send calls started.
        /// </summary>
        private int _attempts;

        /// <summary>
        /// Gets the delay every send holds before completing.
        /// </summary>
        /// <value>Zero by default.</value>
        public TimeSpan SendDelay { get; init; }

        /// <summary>
        /// Gets the exception every send throws, when set.
        /// </summary>
        /// <value><see langword="null"/> when sends succeed.</value>
        public Exception? SendFailure { get; init; }

        /// <summary>
        /// Gets the task every send waits for before completing, when set.
        /// </summary>
        /// <value><see langword="null"/> when sends do not wait.</value>
        public Task? SendGate { get; init; }

        /// <summary>
        /// Gets the frames delivered so far as UTF-8 text.
        /// </summary>
        /// <value>A snapshot-safe list; read it only once sends finished.</value>
        public List<string> Frames { get; } = [];

        /// <summary>
        /// Gets the number of overlapping sends observed.
        /// </summary>
        /// <value>Zero when sends were serialized.</value>
        public int OverlapCount => Volatile.Read(ref _overlaps);

        /// <summary>
        /// Gets the number of send calls started.
        /// </summary>
        /// <value>The count of started sends, successful or not.</value>
        public int SendAttempts => Volatile.Read(ref _attempts);

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
            => SendCoreAsync(buffer);

        /// <inheritdoc />
        public override ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
            => new(SendCoreAsync(buffer.ToArray()));

        /// <summary>
        /// Records one frame, throwing when another send is still in flight.
        /// </summary>
        /// <param name="buffer">The frame bytes.</param>
        /// <returns>A task completing once the simulated send finished.</returns>
        /// <exception cref="InvalidOperationException">Another send was still in flight.</exception>
        private async Task SendCoreAsync(ReadOnlyMemory<byte> buffer)
        {
            Interlocked.Increment(ref _attempts);
            if (Interlocked.Increment(ref _inFlight) > 1)
            {
                Interlocked.Decrement(ref _inFlight);
                Interlocked.Increment(ref _overlaps);
                throw new InvalidOperationException("A send is already in progress.");
            }

            try
            {
                if (SendFailure is not null)
                    throw SendFailure;
                if (SendGate is not null)
                    await SendGate.ConfigureAwait(false);
                if (SendDelay > TimeSpan.Zero)
                    await Task.Delay(SendDelay).ConfigureAwait(false);
                lock (Frames)
                    Frames.Add(Encoding.UTF8.GetString(buffer.Span));
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }
}
