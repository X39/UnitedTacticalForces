using System.Net.WebSockets;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Opens the outbound WebSocket to the control plane. Abstracted so tests can hand the client an in-memory or
/// loopback socket.
/// </summary>
public interface IWorkerChannelConnector
{
    /// <summary>
    /// Opens one connection.
    /// </summary>
    /// <param name="uri">The channel URI. It never contains the credential.</param>
    /// <param name="credential">The worker credential to present as a bearer token in the Authorization header.</param>
    /// <param name="cancellationToken">A token that aborts the connect attempt.</param>
    /// <returns>An open WebSocket that the caller owns.</returns>
    /// <exception cref="WorkerChannelAuthenticationException">The control plane answered the upgrade with 401.</exception>
    /// <exception cref="WorkerChannelUnavailableException">The connection could not be established.</exception>
    Task<WebSocket> ConnectAsync(Uri uri, string credential, CancellationToken cancellationToken);
}
