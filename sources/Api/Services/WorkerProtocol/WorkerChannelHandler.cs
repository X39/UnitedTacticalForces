using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Serves the persistent worker channel: receives frames, executes them through their routes and answers each one
/// with a correlated ack.
/// </summary>
/// <remarks>
/// The handler owns transport concerns only. Every decision, including claim ordering, leases, supersession and
/// credential checks, stays in <see cref="IWorkerProtocolService"/> behind the routes. Frames are processed one at a
/// time per connection. Every send goes through the connection wrapper, so an ack never interleaves with a frame the
/// control plane pushes through the registry. Frame bodies, command payloads and credentials are never logged.
/// </remarks>
/// <param name="scopeFactory">Creates the dependency injection scope each frame is executed in.</param>
/// <param name="registry">Holds the live connection of each worker so the control plane can push to it.</param>
/// <param name="options">The channel settings.</param>
/// <param name="logger">The logger; it receives worker ids, frame types and outcomes only.</param>
/// <param name="timeProvider">The clock of the frame rate limit; the system clock when the container supplies none.</param>
[Singleton<WorkerChannelHandler, IWorkerChannelHandler>]
internal sealed class WorkerChannelHandler(
    IServiceScopeFactory scopeFactory,
    IWorkerChannelRegistry registry,
    IOptions<LifecycleWorkerOptions> options,
    ILogger<WorkerChannelHandler> logger,
    TimeProvider? timeProvider = null) : IWorkerChannelHandler
{
    /// <summary>The size of the buffer a socket receive reads into.</summary>
    private const int ReceiveChunkBytes = 4096;

    /// <summary>The longest error text an ack carries, matching the frame contract.</summary>
    private const int MaxErrorLength = 512;

    /// <summary>The JSON depth limit applied when reading only the id of an unreadable frame.</summary>
    private const int MaxJsonDepth = 32;

    /// <summary>The ack error for a frame the channel cannot route.</summary>
    private const string UnsupportedFrameError = "The frame type is unsupported or the frame is invalid.";

    /// <summary>The generic ack error for an unexpected failure; it never carries exception text.</summary>
    private const string UnavailableError = "The control plane could not process the request.";

    /// <summary>The close description sent when a worker exhausts its frame token bucket.</summary>
    private const string FrameRateExceededDescription = "frame rate exceeded";

    /// <inheritdoc />
    public async Task RunAsync(
        AuthenticatedWorker worker,
        string credential,
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var connection = new WorkerChannelConnection(
            socket,
            worker.WorkerId,
            worker.DeploymentId,
            options.Value.ChannelSendTimeout);
        var bucket = new TokenBucket(
            options.Value.ChannelFrameBurst,
            options.Value.ChannelMaxFramesPerSecond,
            timeProvider ?? TimeProvider.System);
        IDisposable? registration = null;
        try
        {
            registration = registry.Register(connection);
            await connection.SendAsync(
                    new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake },
                    cancellationToken)
                .ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested && socket.State is WebSocketState.Open)
            {
                var message = await ReceiveMessageAsync(socket, cancellationToken).ConfigureAwait(false);
                if (message.IsClose)
                    break;
                if (!bucket.TryTake())
                {
                    logger.LogWarning(
                        "Worker channel of {WorkerId} exceeded the frame rate limit; closing the channel",
                        worker.WorkerId);
                    await connection
                        .CloseAsync(WebSocketCloseStatus.PolicyViolation, FrameRateExceededDescription, CancellationToken.None)
                        .ConfigureAwait(false);
                    return;
                }
                if (message.IsTooBig)
                {
                    logger.LogWarning(
                        "Worker channel message from {WorkerId} exceeded the size cap",
                        worker.WorkerId);
                    await connection
                        .CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too big.", CancellationToken.None)
                        .ConfigureAwait(false);
                    return;
                }
                if (message.Bytes is null)
                    continue;

                var keepOpen = await ProcessMessageAsync(
                        worker,
                        credential,
                        connection,
                        message.Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!keepOpen)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            // The request was aborted or a stuck send aborted the connection; both end the channel.
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            registration?.Dispose();
            await CloseIfOpenAsync(connection, socket, cancellationToken.IsCancellationRequested)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deserializes one received message, executes it through its route and sends the ack.
    /// </summary>
    /// <param name="worker">The authenticated worker the frame is attributed to.</param>
    /// <param name="credential">The held worker credential.</param>
    /// <param name="connection">The connection the ack is sent on.</param>
    /// <param name="bytes">The complete UTF-8 text of the received message.</param>
    /// <param name="cancellationToken">A token cancelling the execution.</param>
    /// <returns>
    /// True when the channel stays open; false when the worker credential was lost and the socket was closed.
    /// </returns>
    private async Task<bool> ProcessMessageAsync(
        AuthenticatedWorker worker,
        string credential,
        IWorkerChannelConnection connection,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        WorkerChannelFrame? frame;
        try
        {
            frame = JsonSerializer.Deserialize<WorkerChannelFrame>(bytes, WorkerChannelJson.Options);
        }
        catch (JsonException)
        {
            // Unknown enum names and malformed members land here. Answer when the correlation id is readable so the
            // worker is not left waiting; a message without one cannot be answered and is ignored.
            if (TryReadId(bytes) is { } unreadableId)
            {
                await SendAckAsync(
                        connection,
                        worker,
                        null,
                        unreadableId,
                        null,
                        EWorkerChannelOutcome.Rejected,
                        UnsupportedFrameError,
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            return true;
        }
        if (frame?.Id is not { } id)
            return true;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var route = scope.ServiceProvider.GetServices<IWorkerChannelFrameRoute>()
                .FirstOrDefault(q => q.FrameTypes.Contains(frame.Type));
            if (route is null)
            {
                await SendAckAsync(
                        connection,
                        worker,
                        frame.Type,
                        id,
                        frame.CommandId,
                        EWorkerChannelOutcome.Rejected,
                        UnsupportedFrameError,
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var result = await route
                .HandleAsync(new WorkerChannelRequest(worker, credential, frame), cancellationToken)
                .ConfigureAwait(false);
            await SendAckAsync(
                    connection,
                    worker,
                    frame.Type,
                    id,
                    frame.CommandId,
                    EWorkerChannelOutcome.Accepted,
                    null,
                    result.Result is null
                        ? null
                        : JsonSerializer.SerializeToElement(result.Result, WorkerChannelJson.Options),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.WakeAfterAck)
            {
                await connection.SendAsync(
                        new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            return true;
        }
        catch (WorkerProtocolAuthenticationException)
        {
            logger.LogWarning(
                "Worker channel credential of {WorkerId} was rejected on frame {FrameType}; closing the channel",
                worker.WorkerId,
                frame.Type);
            await connection
                .CloseAsync(WebSocketCloseStatus.PolicyViolation, "Credential rejected.", CancellationToken.None)
                .ConfigureAwait(false);
            return false;
        }
        catch (WorkerProtocolConflictException ex)
        {
            await SendAckAsync(
                    connection,
                    worker,
                    frame.Type,
                    id,
                    frame.CommandId,
                    EWorkerChannelOutcome.Superseded,
                    HostingFailureSanitizer.Sanitize(ex.Message, MaxErrorLength),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is WorkerProtocolValidationException or WorkerProtocolNotFoundException)
        {
            await SendAckAsync(
                    connection,
                    worker,
                    frame.Type,
                    id,
                    frame.CommandId,
                    EWorkerChannelOutcome.Rejected,
                    HostingFailureSanitizer.Sanitize(ex.Message, MaxErrorLength),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not WebSocketException)
        {
            logger.LogError(
                "Worker channel frame failed for {WorkerId} ({FrameType}): {Error}",
                worker.WorkerId,
                frame.Type,
                HostingFailureSanitizer.Sanitize(ex.Message));
            await SendAckAsync(
                    connection,
                    worker,
                    frame.Type,
                    id,
                    frame.CommandId,
                    EWorkerChannelOutcome.Unavailable,
                    UnavailableError,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Sends an ack that echoes the request id and logs the outcome without the frame body.
    /// </summary>
    /// <param name="connection">The connection to send on.</param>
    /// <param name="worker">The worker the frame is attributed to, for the log entry.</param>
    /// <param name="frameType">The type of the answered frame, or null when it could not be read.</param>
    /// <param name="id">The request id to echo.</param>
    /// <param name="commandId">The command id of the answered frame, echoed for report frames.</param>
    /// <param name="outcome">How the request was resolved.</param>
    /// <param name="error">The bounded error text, or null for an accepted request.</param>
    /// <param name="payload">The result payload, or null when there is none.</param>
    /// <param name="cancellationToken">A token cancelling the send.</param>
    /// <returns>A task completing once the ack was written.</returns>
    private Task SendAckAsync(
        IWorkerChannelConnection connection,
        AuthenticatedWorker worker,
        EWorkerChannelFrameType? frameType,
        Guid id,
        Guid? commandId,
        EWorkerChannelOutcome outcome,
        string? error,
        JsonElement? payload,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Worker channel frame {FrameType} from {WorkerId} was {Outcome}",
            frameType,
            worker.WorkerId,
            outcome);
        return connection.SendAsync(
            new WorkerChannelFrame
            {
                Type = EWorkerChannelFrameType.Ack,
                Id = id,
                CommandId = commandId,
                Outcome = outcome,
                Error = error,
                Payload = payload,
            },
            cancellationToken);
    }

    /// <summary>
    /// Reads the correlation id from a message that could not be deserialized as a frame.
    /// </summary>
    /// <param name="bytes">The UTF-8 text of the message.</param>
    /// <returns>The id when the message is a JSON object with a GUID id member; otherwise null.</returns>
    private static Guid? TryReadId(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind is JsonValueKind.String
                    && property.Value.TryGetGuid(out var id))
                {
                    return id;
                }
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    /// <summary>
    /// Receives one complete text message, assembling fragments, up to the configured message cap.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <param name="cancellationToken">A token cancelling the receive.</param>
    /// <returns>The received message, a close indication or a too-big indication.</returns>
    private async Task<ReceivedMessage> ReceiveMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var maxBytes = options.Value.ChannelMaxMessageBytes;
        var buffer = new byte[ReceiveChunkBytes];
        var writer = new ArrayBufferWriter<byte>();
        var isText = true;
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (result.MessageType is WebSocketMessageType.Close)
                return ReceivedMessage.Close;
            isText = result.MessageType is WebSocketMessageType.Text;
            if (writer.WrittenCount + result.Count > maxBytes)
                return ReceivedMessage.TooBig;
            writer.Write(buffer.AsSpan(0, result.Count));
        }
        while (!result.EndOfMessage);

        return isText ? new ReceivedMessage(writer.WrittenSpan.ToArray(), false, false) : ReceivedMessage.Ignored;
    }

    /// <summary>
    /// Closes the connection when the loop ended without closing it.
    /// </summary>
    /// <param name="connection">The connection that sends the close.</param>
    /// <param name="socket">The socket, inspected to skip a close that already happened.</param>
    /// <param name="wasCancelled">True when the channel ended because the request was aborted.</param>
    /// <returns>A task completing once the socket was closed or nothing was left to close.</returns>
    private static Task CloseIfOpenAsync(IWorkerChannelConnection connection, WebSocket socket, bool wasCancelled)
        => socket.State is WebSocketState.Open or WebSocketState.CloseReceived
            ? connection.CloseAsync(
                wasCancelled ? WebSocketCloseStatus.EndpointUnavailable : WebSocketCloseStatus.NormalClosure,
                wasCancelled ? "Control plane unavailable." : "Closed.",
                CancellationToken.None)
            : Task.CompletedTask;

    /// <summary>
    /// A per-connection token bucket that limits how many messages a worker may send.
    /// </summary>
    /// <remarks>
    /// The bucket starts full and refills continuously from the injected clock, so a burst up to the capacity is
    /// accepted and the sustained rate is capped. It is used by the single receive loop of one connection and needs no
    /// locking.
    /// </remarks>
    private sealed class TokenBucket
    {
        private readonly double _capacity;
        private readonly double _refillPerSecond;
        private readonly TimeProvider _clock;
        private double _tokens;
        private long _lastRefill;

        /// <summary>
        /// Creates a full bucket.
        /// </summary>
        /// <param name="capacity">The burst size; values below one are raised to one.</param>
        /// <param name="refillPerSecond">The sustained messages per second; values below one are raised to one.</param>
        /// <param name="clock">The clock the refill is measured against.</param>
        public TokenBucket(int capacity, int refillPerSecond, TimeProvider clock)
        {
            _capacity = Math.Max(1, capacity);
            _refillPerSecond = Math.Max(1, refillPerSecond);
            _clock = clock;
            _tokens = _capacity;
            _lastRefill = clock.GetTimestamp();
        }

        /// <summary>
        /// Refills the bucket for the elapsed time and takes one token when one is available.
        /// </summary>
        /// <returns>True when a token was taken; false when the rate limit is exceeded.</returns>
        public bool TryTake()
        {
            var now = _clock.GetTimestamp();
            _tokens = Math.Min(_capacity, _tokens + _clock.GetElapsedTime(_lastRefill, now).TotalSeconds * _refillPerSecond);
            _lastRefill = now;
            if (_tokens < 1)
                return false;
            _tokens -= 1;
            return true;
        }
    }

    /// <summary>
    /// The outcome of receiving one message from the worker.
    /// </summary>
    /// <param name="Bytes">The UTF-8 text of a complete message, or null for a close, oversize or ignored message.</param>
    /// <param name="IsClose">True when the worker sent a close frame.</param>
    /// <param name="IsTooBig">True when the message exceeded the configured cap.</param>
    private sealed record ReceivedMessage(byte[]? Bytes, bool IsClose, bool IsTooBig)
    {
        /// <summary>Gets the result for a close frame.</summary>
        /// <value>A message flagged as close.</value>
        public static ReceivedMessage Close { get; } = new(null, true, false);

        /// <summary>Gets the result for a message above the cap.</summary>
        /// <value>A message flagged as too big.</value>
        public static ReceivedMessage TooBig { get; } = new(null, false, true);

        /// <summary>Gets the result for a message the channel does not process, such as a binary message.</summary>
        /// <value>A message without bytes that is neither a close nor too big.</value>
        public static ReceivedMessage Ignored { get; } = new(null, false, false);
    }
}
