using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace CloudWorker.Tests;

/// <summary>
/// A scriptable control-plane stand-in that implements <see cref="IWorkerChannelConnector"/>: it hands the client end
/// of a real loopback WebSocket to the client under test, keeps the server end, records every frame and credential,
/// and can ack, push a wake, close with a status, refuse the upgrade or send an oversized message.
/// </summary>
internal sealed class FakeWorkerChannelServer : IWorkerChannelConnector
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private readonly List<ServerConnection> _connections = [];
    private readonly List<string> _credentials = [];
    private int _attempts;
    private int _failConnects;
    private int _refuseConnects;

    /// <summary>Gets the number of connect attempts the server has seen, including refused ones.</summary>
    /// <value>The attempt count.</value>
    public int ConnectAttempts
    {
        get
        {
            lock (_gate)
                return _attempts;
        }
    }

    /// <summary>Gets the credential presented on each connect attempt, in order.</summary>
    /// <value>A snapshot of the credentials.</value>
    public IReadOnlyList<string> Credentials
    {
        get
        {
            lock (_gate)
                return [.. _credentials];
        }
    }

    /// <summary>Makes the next connect attempts fail with a transient error.</summary>
    /// <param name="count">The number of attempts to fail.</param>
    public void FailNextConnects(int count)
    {
        lock (_gate)
            _failConnects = count;
    }

    /// <summary>Makes the next connect attempts fail as an HTTP 401 at upgrade.</summary>
    /// <param name="count">The number of attempts to refuse.</param>
    public void RefuseNextConnects(int count)
    {
        lock (_gate)
            _refuseConnects = count;
    }

    /// <inheritdoc />
    public async Task<WebSocket> ConnectAsync(Uri uri, string credential, CancellationToken cancellationToken)
    {
        bool refuse, fail;
        lock (_gate)
        {
            _attempts++;
            _credentials.Add(credential);
            refuse = _refuseConnects > 0;
            if (refuse)
                _refuseConnects--;
            fail = !refuse && _failConnects > 0;
            if (fail)
                _failConnects--;
        }
        if (refuse)
            throw new WorkerChannelAuthenticationException();
        if (fail)
            throw new WorkerChannelUnavailableException("The fake server refused the connection.");

        GatedStream? gate = null;
        var pair = await LoopbackWebSocketPair.CreateAsync(stream => gate = new GatedStream(stream))
            .ConfigureAwait(false);
        var connection = new ServerConnection(pair.Server, uri, credential, gate!);
        lock (_gate)
            _connections.Add(connection);
        return pair.Client;
    }

    /// <summary>
    /// Waits for the connection with the given zero-based index to be accepted.
    /// </summary>
    /// <param name="index">The zero-based connection index.</param>
    /// <returns>The server side of that connection.</returns>
    /// <exception cref="TimeoutException">The connection was not accepted in time.</exception>
    public async Task<ServerConnection> WaitForConnectionAsync(int index)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (DateTime.UtcNow < deadline)
        {
            lock (_gate)
            {
                if (_connections.Count > index)
                    return _connections[index];
            }
            await Task.Delay(5).ConfigureAwait(false);
        }
        throw new TimeoutException($"Connection {index} was not accepted.");
    }

    /// <summary>
    /// The server end of one accepted connection.
    /// </summary>
    internal sealed class ServerConnection
    {
        private readonly Channel<WorkerChannelFrame> _frames = Channel.CreateUnbounded<WorkerChannelFrame>();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly GatedStream _gate;
        private readonly TaskCompletionSource<WebSocketCloseStatus?> _closed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Initializes a new instance of the <see cref="ServerConnection"/> class and starts reading.</summary>
        /// <param name="socket">The server-side socket.</param>
        /// <param name="uri">The URI the client connected to.</param>
        /// <param name="credential">The credential the client presented.</param>
        /// <param name="gate">The client write gate.</param>
        public ServerConnection(WebSocket socket, Uri uri, string credential, GatedStream gate)
        {
            Socket = socket;
            Uri = uri;
            Credential = credential;
            _gate = gate;
            _ = Task.Run(ReadLoopAsync);
        }

        /// <summary>Gets the server-side socket.</summary>
        /// <value>The socket.</value>
        public WebSocket Socket { get; }

        /// <summary>Gets the URI the client connected to.</summary>
        /// <value>The channel URI.</value>
        public Uri Uri { get; }

        /// <summary>Gets the credential the client presented for this connection.</summary>
        /// <value>The credential.</value>
        public string Credential { get; }

        /// <summary>Gets a task that completes with the close status once the connection ended.</summary>
        /// <value>The status the client closed with, or null when the connection ended abnormally.</value>
        public Task<WebSocketCloseStatus?> Closed => _closed.Task;

        /// <summary>Stalls every further client write on this connection.</summary>
        public void BlockClientWrites() => _gate.BlockWrites = true;

        /// <summary>Reads the next frame the client sent.</summary>
        /// <returns>The frame.</returns>
        /// <exception cref="TimeoutException">No frame arrived in time.</exception>
        public async Task<WorkerChannelFrame> ReadFrameAsync()
        {
            using var cts = new CancellationTokenSource(WaitLimit);
            try
            {
                return await _frames.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("No frame arrived.");
            }
        }

        /// <summary>Checks that no frame arrives within a short period.</summary>
        /// <param name="period">How long to watch.</param>
        /// <returns><see langword="true"/> when no frame arrived.</returns>
        public async Task<bool> StaysSilentAsync(TimeSpan period)
        {
            using var cts = new CancellationTokenSource(period);
            try
            {
                await _frames.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        /// <summary>Answers a request frame with an ack carrying the same id.</summary>
        /// <param name="request">The request being answered.</param>
        /// <param name="outcome">The outcome to report.</param>
        /// <param name="payload">An optional result payload.</param>
        /// <param name="error">An optional error text.</param>
        /// <returns>A task that completes when the ack was sent.</returns>
        public Task AckAsync(
            WorkerChannelFrame request,
            EWorkerChannelOutcome outcome = EWorkerChannelOutcome.Accepted,
            object? payload = null,
            string? error = null)
            => SendAsync(new WorkerChannelFrame
            {
                Type    = EWorkerChannelFrameType.Ack,
                Id      = request.Id,
                Outcome = outcome,
                Error   = error,
                Payload = payload is null
                    ? null
                    : JsonSerializer.SerializeToElement(payload, payload.GetType(), CloudWorkerJson.Options),
            });

        /// <summary>Pushes a wake frame.</summary>
        /// <returns>A task that completes when the frame was sent.</returns>
        public Task SendWakeAsync() => SendAsync(new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake });

        /// <summary>Closes the connection from the server side.</summary>
        /// <param name="status">The close status to send.</param>
        /// <returns>A task that completes when the close frame was sent.</returns>
        public async Task CloseAsync(WebSocketCloseStatus status)
        {
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await Socket.CloseOutputAsync(status, "test", CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>Sends one text message of the given size.</summary>
        /// <param name="bytes">The message size in bytes.</param>
        /// <returns>A task that completes when the message was sent or the client went away.</returns>
        public async Task SendOversizedAsync(int bytes)
        {
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var message = new byte[bytes];
                Array.Fill(message, (byte)'a');
                await Socket.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or IOException)
            {
                // The client closes as soon as the cap is exceeded, so the tail of the message can fail.
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task SendAsync(WorkerChannelFrame frame)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, CloudWorkerJson.Options);
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReadLoopAsync()
        {
            var buffer = new byte[16384];
            using var message = new MemoryStream();
            try
            {
                while (true)
                {
                    var result = await Socket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None)
                        .ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _closed.TrySetResult(Socket.CloseStatus);
                        if (Socket.State == WebSocketState.CloseReceived)
                            await CloseAsync(WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
                        return;
                    }
                    message.Write(buffer, 0, result.Count);
                    if (!result.EndOfMessage)
                        continue;
                    var frame = JsonSerializer.Deserialize<WorkerChannelFrame>(
                        message.ToArray(),
                        CloudWorkerJson.Options);
                    message.SetLength(0);
                    if (frame is not null)
                        _frames.Writer.TryWrite(frame);
                }
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
            {
                _closed.TrySetResult(null);
            }
        }
    }
}
