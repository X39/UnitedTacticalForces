using System.Net.WebSockets;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// The process-local registry of live worker channel connections; the newest connection of a worker wins.
/// </summary>
/// <remarks>
/// The registry holds connections of this API process only, the same accepted single-instance limitation as the
/// fleet notifier. Log entries carry worker ids, deployment ids and frame types only, never a frame body or a
/// credential.
/// </remarks>
/// <param name="logger">The logger for replaced connections and failed pushes.</param>
[Singleton<WorkerChannelRegistry, IWorkerChannelRegistry>]
internal sealed class WorkerChannelRegistry(ILogger<WorkerChannelRegistry> logger) : IWorkerChannelRegistry
{
    /// <summary>The close description sent to a connection that a newer connection of the same worker replaced.</summary>
    private const string ReplacedDescription = "replaced by a newer connection";

    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, IWorkerChannelConnection> _connections = [];

    /// <inheritdoc />
    public int ConnectionCount
    {
        get
        {
            lock (_gate)
                return _connections.Count;
        }
    }

    /// <inheritdoc />
    public IDisposable Register(IWorkerChannelConnection connection)
    {
        IWorkerChannelConnection? replaced;
        lock (_gate)
        {
            _connections.TryGetValue(connection.WorkerId, out replaced);
            _connections[connection.WorkerId] = connection;
        }

        if (replaced is not null && !ReferenceEquals(replaced, connection))
        {
            logger.LogInformation(
                "Worker channel of {WorkerId} replaced by a newer connection",
                connection.WorkerId);
            _ = CloseReplacedAsync(replaced);
        }
        return new Registration(this, connection);
    }

    /// <inheritdoc />
    public async Task<bool> TrySendAsync(Guid workerId, WorkerChannelFrame frame, CancellationToken cancellationToken)
    {
        IWorkerChannelConnection? connection;
        lock (_gate)
            _connections.TryGetValue(workerId, out connection);
        return connection is not null && await TrySendAsync(connection, frame, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendToDeploymentAsync(
        Guid deploymentId,
        WorkerChannelFrame frame,
        CancellationToken cancellationToken)
    {
        IWorkerChannelConnection[] targets;
        lock (_gate)
            targets = _connections.Values.Where(q => q.DeploymentId == deploymentId).ToArray();
        await Task.WhenAll(targets.Select(q => TrySendAsync(q, frame, cancellationToken))).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a frame to one connection and turns every failure except caller cancellation into a logged false.
    /// </summary>
    /// <param name="connection">The connection to send on.</param>
    /// <param name="frame">The frame to send.</param>
    /// <param name="cancellationToken">A token cancelling the send.</param>
    /// <returns>True when the frame was written; false when the send failed.</returns>
    private async Task<bool> TrySendAsync(
        IWorkerChannelConnection connection,
        WorkerChannelFrame frame,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Worker channel push of {FrameType} to {WorkerId} of deployment {DeploymentId} failed ({Reason})",
                frame.Type,
                connection.WorkerId,
                connection.DeploymentId,
                ex.GetType().Name);
            Remove(connection);
            return false;
        }
    }

    /// <summary>
    /// Closes a replaced connection without ever throwing into the registering caller.
    /// </summary>
    /// <param name="replaced">The connection that lost its registration.</param>
    /// <returns>A task completing once the close was attempted.</returns>
    private async Task CloseReplacedAsync(IWorkerChannelConnection replaced)
    {
        try
        {
            await replaced
                .CloseAsync(WebSocketCloseStatus.NormalClosure, ReplacedDescription, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Closing the replaced worker channel of {WorkerId} failed ({Reason})",
                replaced.WorkerId,
                ex.GetType().Name);
        }
    }

    /// <summary>
    /// Removes a connection from the registry while it is still the registered one of its worker.
    /// </summary>
    /// <param name="connection">The connection to remove.</param>
    private void Remove(IWorkerChannelConnection connection)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connection.WorkerId, out var current) && ReferenceEquals(current, connection))
                _connections.Remove(connection.WorkerId);
        }
    }

    /// <summary>
    /// The handle returned by <see cref="Register"/>.
    /// </summary>
    /// <param name="registry">The registry the connection was registered in.</param>
    /// <param name="connection">The registered connection.</param>
    private sealed class Registration(WorkerChannelRegistry registry, IWorkerChannelConnection connection)
        : IDisposable
    {
        /// <summary>Removes the registration while it still refers to the connection; repeated calls are no-ops.</summary>
        public void Dispose() => registry.Remove(connection);
    }
}
