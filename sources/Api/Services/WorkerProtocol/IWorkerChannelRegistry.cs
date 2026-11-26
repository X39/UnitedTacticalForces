using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Tracks the live worker channel connection of each worker so any control-plane component can push a frame to a
/// connected worker without polling.
/// </summary>
/// <remarks>
/// The registry is process-local: it knows only connections held by this API instance. With several API instances a
/// push reaches a worker only when it is connected to the instance that pushes; the worker's own periodic claim keeps
/// working as the fallback. The registry only sends what a caller hands it and never pushes on its own.
/// </remarks>
internal interface IWorkerChannelRegistry
{
    /// <summary>Gets the number of workers that currently hold a registered connection.</summary>
    /// <value>The count of registered connections, at most one per worker.</value>
    int ConnectionCount { get; }

    /// <summary>
    /// Registers a connection as the live connection of its worker.
    /// </summary>
    /// <remarks>
    /// A previously registered connection of the same worker is replaced and closed with a normal closure: a
    /// reconnecting worker must never be locked out by its own half-dead socket.
    /// </remarks>
    /// <param name="connection">The connection to register.</param>
    /// <returns>
    /// A handle whose disposal removes the registration, but only while it still refers to
    /// <paramref name="connection"/>; disposing the handle of a replaced connection never removes the newer one.
    /// </returns>
    IDisposable Register(IWorkerChannelConnection connection);

    /// <summary>
    /// Pushes a frame to the connection of one worker.
    /// </summary>
    /// <param name="workerId">The worker to push to.</param>
    /// <param name="frame">The frame to send.</param>
    /// <param name="cancellationToken">A token cancelling the push.</param>
    /// <returns>
    /// True when the worker had a connection and the frame was written; false when it has none or the send failed.
    /// </returns>
    /// <exception cref="OperationCanceledException">The caller cancelled the push.</exception>
    Task<bool> TrySendAsync(Guid workerId, WorkerChannelFrame frame, CancellationToken cancellationToken);

    /// <summary>
    /// Pushes a frame to every connected worker of one deployment.
    /// </summary>
    /// <remarks>A failed send is logged and never thrown; a connection that is absent is skipped.</remarks>
    /// <param name="deploymentId">The deployment whose connections receive the frame.</param>
    /// <param name="frame">The frame to send.</param>
    /// <param name="cancellationToken">A token cancelling the push.</param>
    /// <returns>A task completing once every send finished.</returns>
    /// <exception cref="OperationCanceledException">The caller cancelled the push.</exception>
    Task SendToDeploymentAsync(Guid deploymentId, WorkerChannelFrame frame, CancellationToken cancellationToken);
}
