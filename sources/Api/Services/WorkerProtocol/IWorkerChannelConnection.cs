using System.Net.WebSockets;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// One live worker channel connection that frames can be pushed to and that can be closed from the control plane.
/// </summary>
/// <remarks>
/// Implementations serialize sends, so concurrent callers (the channel handler answering a frame and a control-plane
/// push) never interleave on the same socket.
/// </remarks>
internal interface IWorkerChannelConnection
{
    /// <summary>Gets the worker the connection belongs to.</summary>
    /// <value>The id of the authenticated worker.</value>
    Guid WorkerId { get; }

    /// <summary>Gets the deployment the worker is bound to.</summary>
    /// <value>The id of the deployment the worker credential is scoped to.</value>
    Guid DeploymentId { get; }

    /// <summary>
    /// Serializes a frame and sends it as one text message, after every send that was started earlier.
    /// </summary>
    /// <param name="frame">The frame to send.</param>
    /// <param name="cancellationToken">A token cancelling the send.</param>
    /// <returns>A task completing once the frame was written.</returns>
    /// <exception cref="OperationCanceledException">
    /// The caller cancelled the send, or the send did not finish within the configured send timeout and the
    /// connection was aborted.
    /// </exception>
    /// <exception cref="WebSocketException">The socket failed while sending.</exception>
    /// <exception cref="ObjectDisposedException">The socket was already disposed.</exception>
    Task SendAsync(WorkerChannelFrame frame, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the connection from the control plane side without waiting for the peer, on a best-effort basis.
    /// </summary>
    /// <remarks>This method never throws for a socket that is already closed, broken or disposed.</remarks>
    /// <param name="status">The close status to send.</param>
    /// <param name="description">The short, non-sensitive close description.</param>
    /// <param name="cancellationToken">A token cancelling the close attempt.</param>
    /// <returns>A task completing once the close frame was sent or the attempt was abandoned.</returns>
    Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken);
}
