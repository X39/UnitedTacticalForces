using System.Net.WebSockets;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Wraps the accepted WebSocket of one worker channel with a single send lock and a send timeout.
/// </summary>
/// <remarks>
/// A WebSocket allows one send at a time, so every send and every server-initiated close takes the same
/// <see cref="SemaphoreSlim"/>. A send, or the wait for the lock, that does not finish within the send timeout aborts
/// the socket: a peer that stopped reading must not hold the connection, and everything waiting behind it, forever.
/// The send lock is intentionally never disposed; a sender still queued on it may release it afterwards and a
/// semaphore whose wait handle was never accessed holds no unmanaged resource.
/// </remarks>
internal sealed class WorkerChannelConnection : IWorkerChannelConnection
{
    private readonly WebSocket _socket;
    private readonly TimeSpan _sendTimeout;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>
    /// Creates a connection around an accepted socket.
    /// </summary>
    /// <param name="socket">The accepted WebSocket.</param>
    /// <param name="workerId">The authenticated worker.</param>
    /// <param name="deploymentId">The deployment the worker is bound to.</param>
    /// <param name="sendTimeout">
    /// The longest a send may take before the socket is aborted, normally
    /// <see cref="X39.UnitedTacticalForces.Api.Services.GameServerLifecycle.LifecycleWorkerOptions.ChannelSendTimeout"/>.
    /// </param>
    public WorkerChannelConnection(WebSocket socket, Guid workerId, Guid deploymentId, TimeSpan sendTimeout)
    {
        _socket = socket;
        WorkerId = workerId;
        DeploymentId = deploymentId;
        _sendTimeout = sendTimeout;
    }

    /// <inheritdoc />
    public Guid WorkerId { get; }

    /// <inheritdoc />
    public Guid DeploymentId { get; }

    /// <inheritdoc />
    public async Task SendAsync(WorkerChannelFrame frame, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, WorkerChannelJson.Options);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_sendTimeout);
        var hasLock = false;
        try
        {
            await _sendLock.WaitAsync(linked.Token).ConfigureAwait(false);
            hasLock = true;
            await _socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Abort();
            throw new OperationCanceledException("The worker channel send timed out and the connection was aborted.");
        }
        finally
        {
            if (hasLock)
                _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_sendTimeout);
        var hasLock = false;
        try
        {
            await _sendLock.WaitAsync(linked.Token).ConfigureAwait(false);
            hasLock = true;
            if (_socket.State is WebSocketState.Open)
                await _socket.CloseOutputAsync(status, description, linked.Token).ConfigureAwait(false);
            else if (_socket.State is WebSocketState.CloseReceived)
                await _socket.CloseAsync(status, description, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            if (hasLock)
                _sendLock.Release();
        }
    }

    /// <summary>
    /// Aborts the socket so a pending receive and every queued send end.
    /// </summary>
    private void Abort()
    {
        try
        {
            _socket.Abort();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
