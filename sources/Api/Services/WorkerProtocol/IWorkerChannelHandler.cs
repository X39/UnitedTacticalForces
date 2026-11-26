using System.Net.WebSockets;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Runs the persistent worker channel over an accepted WebSocket.
/// </summary>
/// <remarks>
/// The interface is public only because the worker controller action takes it as a service parameter; its single
/// implementation stays internal.
/// </remarks>
public interface IWorkerChannelHandler
{
    /// <summary>
    /// Serves one worker channel until the worker closes it, the credential is lost, the message cap is exceeded or
    /// the request is aborted.
    /// </summary>
    /// <remarks>
    /// The caller authenticates the worker before accepting the socket. The handler sends a Wake frame, executes every
    /// received frame through the matching route and answers it with an Ack that echoes the request id. It never
    /// throws for socket failures; it returns once the channel ended.
    /// </remarks>
    /// <param name="worker">The identity authenticated before the upgrade.</param>
    /// <param name="credential">
    /// The bearer credential the worker authenticated with. It is passed to the protocol service on every frame and
    /// must never be logged.
    /// </param>
    /// <param name="socket">The accepted WebSocket.</param>
    /// <param name="cancellationToken">A token that ends the channel, normally the request-aborted token.</param>
    /// <returns>A task that completes when the channel ended and the socket was closed where still possible.</returns>
    Task RunAsync(
        AuthenticatedWorker worker,
        string credential,
        WebSocket socket,
        CancellationToken cancellationToken);
}
