using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Self-healing client for the persistent worker channel: one outbound WebSocket carries heartbeats, claims, command
/// reports and metrics as correlated request frames, and latches control-plane wake pushes.
/// </summary>
/// <remarks>
/// <para>
/// Pushes are hints only. The client never claims on its own and never polls: the runtime decides when to claim,
/// usually after <see cref="WaitForWakeAsync"/> returns, so ordering, leases and redelivery stay in the protocol
/// service.
/// </para>
/// <para>
/// A dropped socket fails every pending request with <see cref="WorkerChannelUnavailableException"/>, reconnects with
/// bounded backoff using the credential the provider returns at that moment, and sets the wake latch after each
/// connect so claims resume after a gap. The client never logs frame bodies or credentials; diagnostics name only
/// the connection state and a status code.
/// </para>
/// </remarks>
/// <param name="connector">Opens the outbound WebSocket.</param>
/// <param name="options">Timing and size limits.</param>
/// <param name="timeProvider">The clock used for backoff and timeouts, or <see langword="null"/> for the system clock.</param>
public sealed class CloudWorkerChannelClient(
    IWorkerChannelConnector connector,
    CloudWorkerChannelOptions options,
    TimeProvider? timeProvider = null) : IWorkerControlChannel
{
    private const int ErrorTextLimit = 512;
    private const int ReceiveBufferBytes = 16384;
    private readonly object _gate = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _wake = new(0, 1);

    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CancellationTokenSource? _backoffCts;
    private TaskCompletionSource<Session> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid _deploymentId;
    private bool _disposed;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private Session? _session;

    /// <inheritdoc />
    public Task Completion => _completion.Task;

    /// <summary>
    /// Builds the channel URI for a worker: <c>wss</c> for an <c>https</c> base URL and <c>ws</c> for <c>http</c>,
    /// the worker route plus the channel segment, and the non-secret deployment id as a query parameter.
    /// </summary>
    /// <param name="connection">The worker identity and control-plane location.</param>
    /// <returns>The WebSocket URI. It never contains a credential.</returns>
    public static Uri BuildChannelUri(CloudWorkerConnectionConfig connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var baseUrl = connection.ApiBaseUrl.EndsWith('/') ? connection.ApiBaseUrl : connection.ApiBaseUrl + "/";
        var baseUri = new Uri(baseUrl);
        var path = $"{connection.WorkerApiPath.Trim('/')}/{connection.WorkerId:D}/{WorkerChannelProtocol.ChannelPathSegment}";
        var builder = new UriBuilder(new Uri(baseUri, path))
        {
            Scheme = string.Equals(baseUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Port   = baseUri.IsDefaultPort ? -1 : baseUri.Port,
            Query  = $"{WorkerChannelProtocol.DeploymentIdQueryParameter}={connection.DeploymentId:D}",
        };
        return builder.Uri;
    }

    /// <inheritdoc />
    public Task StartAsync(
        CloudWorkerConnectionConfig connection,
        Func<CancellationToken, Task<string>> credentialProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(credentialProvider);
        var uri = BuildChannelUri(connection);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loopTask is not null)
                throw new InvalidOperationException("The worker channel has already been started.");
            _deploymentId = connection.DeploymentId;
            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _loopCts.Token;
            _loopTask = Task.Run(() => RunAsync(uri, credentialProvider, token), CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task WaitUntilConnectedAsync(CancellationToken cancellationToken)
        => await AcquireSessionAsync(true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task HeartbeatAsync(WorkerHeartbeatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await RequestAsync(EWorkerChannelFrameType.Heartbeat, null, request, true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkerCommandClaimResponse?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var deploymentId = CurrentDeploymentIdOrThrow();
        var ack = await RequestAsync(
                EWorkerChannelFrameType.Claim,
                null,
                new WorkerCommandClaimRequest { DeploymentId = deploymentId },
                true,
                cancellationToken)
            .ConfigureAwait(false);
        if (ack.Outcome == EWorkerChannelOutcome.Superseded
            || ack.Payload is not { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } payload)
        {
            return null;
        }
        try
        {
            return payload.Deserialize<WorkerCommandClaimResponse>(CloudWorkerJson.Options);
        }
        catch (JsonException ex)
        {
            throw new WorkerChannelUnavailableException("The control plane returned an unreadable claim.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReportCommandAsync(
        Guid commandId,
        WorkerCommandReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ack = await RequestAsync(
                WorkerCommandReportMapper.ToFrameType(request.State),
                commandId,
                WorkerCommandReportMapper.ToContract(request),
                true,
                cancellationToken)
            .ConfigureAwait(false);
        return ack.Outcome == EWorkerChannelOutcome.Accepted;
    }

    /// <inheritdoc />
    public async Task SendMetricsAsync(WorkerMetricsSample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        // Metrics are best effort: no wait for a connection, no retry, no queue.
        await RequestAsync(EWorkerChannelFrameType.Metrics, null, sample, false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task WaitForWakeAsync(CancellationToken cancellationToken)
        => _wake.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        Session? session;
        lock (_gate)
            session = _session;
        if (session is null)
        {
            // Not connected: cut a running backoff short so the next attempt uses the new credential at once.
            lock (_gate)
                _backoffCts?.Cancel();
            return;
        }

        session.ReconnectRequested = true;
        await CloseNormallyAsync(session, "credential rotated", cancellationToken).ConfigureAwait(false);
        using var grace = new CancellationTokenSource(options.SendTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, grace.Token);
        try
        {
            await session.Ended.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            session.Abort();
            await session.Ended.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Session? session;
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            session = _session;
            cts = _loopCts;
            loop = _loopTask;
        }

        if (session is not null)
            await CloseNormallyAsync(session, "worker stopping", CancellationToken.None).ConfigureAwait(false);
        if (cts is not null)
            await cts.CancelAsync().ConfigureAwait(false);
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The loop reports its own faults through Completion.
            }
        }
        _completion.TrySetResult();
        cts?.Dispose();
    }

    private async Task RunAsync(Uri uri, Func<CancellationToken, Task<string>> credentialProvider, CancellationToken ct)
    {
        var backoff = options.InitialBackoff;
        var delayBeforeAttempt = false;
        var authRetryUsed = false;
        string? rejectedCredential = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (delayBeforeAttempt)
                {
                    await DelayAsync(backoff, ct).ConfigureAwait(false);
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, options.MaximumBackoff.Ticks));
                }
                delayBeforeAttempt = true;

                string credential;
                try
                {
                    credential = await credentialProvider(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("Cloud worker channel could not read the worker credential; retrying.");
                    continue;
                }

                if (rejectedCredential is not null && string.Equals(rejectedCredential, credential, StringComparison.Ordinal))
                {
                    _completion.TrySetException(new WorkerChannelAuthenticationException());
                    return;
                }

                WebSocket socket;
                try
                {
                    socket = await connector.ConnectAsync(uri, credential, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (WorkerChannelAuthenticationException)
                {
                    Console.Error.WriteLine("Cloud worker channel disconnected (401); reconnecting.");
                    rejectedCredential = credential;
                    if (!authRetryUsed)
                    {
                        authRetryUsed = true;
                        delayBeforeAttempt = false;
                    }
                    continue;
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("Cloud worker channel connect failed; reconnecting.");
                    continue;
                }

                backoff = options.InitialBackoff;
                rejectedCredential = null;
                authRetryUsed = false;
                var session = new Session(socket);
                Publish(session);
                ReleaseWake();
                var closeStatus = await ReceiveLoopAsync(session, ct).ConfigureAwait(false);
                EndSession(session);
                if (ct.IsCancellationRequested)
                    break;
                if (session.ReconnectRequested)
                {
                    delayBeforeAttempt = false;
                    continue;
                }
                Console.Error.WriteLine(
                    $"Cloud worker channel disconnected ({closeStatus?.ToString() ?? "transport error"}); reconnecting.");
                if (closeStatus == WebSocketCloseStatus.PolicyViolation)
                {
                    rejectedCredential = credential;
                    if (!authRetryUsed)
                    {
                        authRetryUsed = true;
                        delayBeforeAttempt = false;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            _completion.TrySetResult();
        }
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_gate)
            _backoffCts = cts;
        try
        {
            await Task.Delay(delay, _time, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // A reconnect request cut the backoff short.
        }
        finally
        {
            lock (_gate)
                _backoffCts = null;
        }
    }

    private void Publish(Session session)
    {
        lock (_gate)
        {
            _session = session;
            _connected.TrySetResult(session);
        }
    }

    private void EndSession(Session session)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_session, session))
            {
                _session = null;
                _connected = new TaskCompletionSource<Session>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        session.End(new WorkerChannelUnavailableException("The worker channel connection was lost."));
        session.Abort();
        session.Socket.Dispose();
        session.Ended.TrySetResult();
    }

    private void ReleaseWake()
    {
        if (_wake.CurrentCount != 0)
            return;
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake is already latched.
        }
    }

    /// <summary>
    /// Reads inbound messages until the connection ends.
    /// </summary>
    /// <returns>The close status of the connection, or <see langword="null"/> when it ended abnormally.</returns>
    private async Task<WebSocketCloseStatus?> ReceiveLoopAsync(Session session, CancellationToken ct)
    {
        var socket = session.Socket;
        var buffer = new byte[ReceiveBufferBytes];
        var message = new ArrayBufferWriter<byte>();
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var status = socket.CloseStatus;
                    if (socket.State == WebSocketState.CloseReceived)
                        await CloseNormallyAsync(session, null, CancellationToken.None).ConfigureAwait(false);
                    return status ?? WebSocketCloseStatus.NormalClosure;
                }
                if (message.WrittenCount + result.Count > options.MaxInboundMessageBytes)
                {
                    await CloseAsync(session, WebSocketCloseStatus.MessageTooBig, "message too big")
                        .ConfigureAwait(false);
                    return WebSocketCloseStatus.MessageTooBig;
                }
                message.Write(buffer.AsSpan(0, result.Count));
                if (!result.EndOfMessage)
                    continue;
                if (result.MessageType == WebSocketMessageType.Text)
                    HandleMessage(session, message.WrittenSpan);
                message.ResetWrittenCount();
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException
                                       or ObjectDisposedException or InvalidOperationException)
        {
            return null;
        }
    }

    private void HandleMessage(Session session, ReadOnlySpan<byte> utf8)
    {
        WorkerChannelFrame? frame;
        try
        {
            frame = JsonSerializer.Deserialize<WorkerChannelFrame>(utf8, CloudWorkerJson.Options);
        }
        catch (JsonException)
        {
            return;
        }
        switch (frame?.Type)
        {
            case EWorkerChannelFrameType.Ack when frame.Id is { } id:
                session.TryComplete(id, frame);
                break;
            case EWorkerChannelFrameType.Wake:
                ReleaseWake();
                break;
        }
    }

    private Guid CurrentDeploymentIdOrThrow()
    {
        lock (_gate)
        {
            return _deploymentId != Guid.Empty
                ? _deploymentId
                : throw new InvalidOperationException("The worker channel has not been started.");
        }
    }

    private async Task<Session> AcquireSessionAsync(bool waitForConnection, CancellationToken ct)
    {
        Task<Session> connected;
        lock (_gate)
        {
            if (!waitForConnection)
            {
                return _session
                       ?? throw new WorkerChannelUnavailableException("The worker channel is not connected.");
            }
            connected = _connected.Task;
        }

        var winner = await Task.WhenAny(connected, _completion.Task).WaitAsync(ct).ConfigureAwait(false);
        if (ReferenceEquals(winner, connected))
            return await connected.ConfigureAwait(false);
        await _completion.Task.ConfigureAwait(false);
        throw new WorkerChannelUnavailableException("The worker channel has stopped.");
    }

    private async Task<WorkerChannelFrame> RequestAsync(
        EWorkerChannelFrameType type,
        Guid? commandId,
        object payload,
        bool waitForConnection,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_completion.Task.IsCompleted)
        {
            await _completion.Task.ConfigureAwait(false);
            throw new WorkerChannelUnavailableException("The worker channel has stopped.");
        }
        var id = Guid.NewGuid();
        var frame = new WorkerChannelFrame
        {
            Type      = type,
            Id        = id,
            CommandId = commandId,
            Payload   = JsonSerializer.SerializeToElement(payload, payload.GetType(), CloudWorkerJson.Options),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, CloudWorkerJson.Options);
        using var deadline = new CancellationTokenSource(options.RequestTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        Session? session = null;
        try
        {
            var pending = new TaskCompletionSource<WorkerChannelFrame>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            while (true)
            {
                session = await AcquireSessionAsync(waitForConnection, linked.Token).ConfigureAwait(false);
                if (session.TryRegister(id, pending))
                    break;
                if (!waitForConnection)
                    throw new WorkerChannelUnavailableException("The worker channel is not connected.");
            }
            await SendAsync(session, bytes, linked.Token).ConfigureAwait(false);
            var ack = await pending.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            return EnsureHandled(ack);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new WorkerChannelUnavailableException("The worker channel request timed out.");
        }
        finally
        {
            session?.Remove(id);
        }
    }

    private static WorkerChannelFrame EnsureHandled(WorkerChannelFrame ack)
    {
        switch (ack.Outcome)
        {
            case EWorkerChannelOutcome.Accepted:
            case EWorkerChannelOutcome.Superseded:
                return ack;
            case EWorkerChannelOutcome.Rejected:
            case EWorkerChannelOutcome.Unavailable:
                var error = WorkerSecretRedactor.Redact(ack.Error) ?? string.Empty;
                if (error.Length > ErrorTextLimit)
                    error = error[..ErrorTextLimit];
                throw new WorkerChannelUnavailableException(
                    error.Length == 0
                        ? $"The control plane did not accept the request ({ack.Outcome})."
                        : $"The control plane did not accept the request ({ack.Outcome}): {error}",
                    ack.Outcome.Value);
            default:
                throw new WorkerChannelUnavailableException("The control plane returned an acknowledgement without an outcome.");
        }
    }

    private async Task SendAsync(Session session, byte[] bytes, CancellationToken ct)
    {
        await session.SendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var timeout = new CancellationTokenSource(options.SendTimeout, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                await session.Socket
                    .SendAsync(bytes, WebSocketMessageType.Text, true, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                session.Abort();
                throw new WorkerChannelUnavailableException("The worker channel send timed out.");
            }
            catch (OperationCanceledException)
            {
                session.Abort();
                throw;
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException
                                           or InvalidOperationException)
            {
                session.Abort();
                throw new WorkerChannelUnavailableException("The worker channel connection was lost.", ex);
            }
        }
        finally
        {
            session.SendLock.Release();
        }
    }

    private Task CloseNormallyAsync(Session session, string? description, CancellationToken ct)
        => CloseAsync(session, WebSocketCloseStatus.NormalClosure, description, ct);

    /// <summary>
    /// Sends a close frame with the send timeout and ignores a connection that is already gone.
    /// </summary>
    private async Task CloseAsync(
        Session session,
        WebSocketCloseStatus status,
        string? description,
        CancellationToken ct = default)
    {
        try
        {
            using var timeout = new CancellationTokenSource(options.SendTimeout, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            await session.SendLock.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                if (session.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await session.Socket.CloseOutputAsync(status, description, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                session.SendLock.Release();
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException
                                       or ObjectDisposedException or InvalidOperationException)
        {
            session.Abort();
        }
    }

    /// <summary>
    /// One open connection: its socket, send lock and the requests waiting for acks on it.
    /// </summary>
    private sealed class Session(WebSocket socket)
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, TaskCompletionSource<WorkerChannelFrame>> _pending = new();
        private bool _ended;

        /// <summary>Gets the connection socket.</summary>
        public WebSocket Socket { get; } = socket;

        /// <summary>Gets the lock that keeps a single writer on the socket.</summary>
        public SemaphoreSlim SendLock { get; } = new(1, 1);

        /// <summary>Gets a task source completed once the connection is fully torn down.</summary>
        public TaskCompletionSource Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets or sets whether the connection is being closed on purpose to reconnect at once.</summary>
        public volatile bool ReconnectRequested;

        /// <summary>Registers a request that waits for an ack, unless the connection has ended.</summary>
        public bool TryRegister(Guid id, TaskCompletionSource<WorkerChannelFrame> pending)
        {
            lock (_gate)
            {
                if (_ended)
                    return false;
                _pending[id] = pending;
                return true;
            }
        }

        /// <summary>Forgets a request.</summary>
        public void Remove(Guid id)
        {
            lock (_gate)
                _pending.Remove(id);
        }

        /// <summary>Completes the request an ack answers; unknown ids are ignored.</summary>
        public void TryComplete(Guid id, WorkerChannelFrame ack)
        {
            TaskCompletionSource<WorkerChannelFrame>? pending;
            lock (_gate)
                _pending.Remove(id, out pending);
            pending?.TrySetResult(ack);
        }

        /// <summary>Marks the connection ended and fails every waiting request.</summary>
        public void End(Exception reason)
        {
            TaskCompletionSource<WorkerChannelFrame>[] pending;
            lock (_gate)
            {
                _ended = true;
                pending = [.. _pending.Values];
                _pending.Clear();
            }
            foreach (var request in pending)
                request.TrySetException(reason);
        }

        /// <summary>Aborts the socket without a close handshake.</summary>
        public void Abort()
        {
            try
            {
                Socket.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
