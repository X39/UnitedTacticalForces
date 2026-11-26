using System.Net;
using System.Net.WebSockets;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Production connector that opens the channel with <see cref="ClientWebSocket"/> and the bearer credential in the
/// Authorization header.
/// </summary>
/// <param name="options">The keep-alive and connect timing.</param>
public sealed class ClientWebSocketChannelConnector(CloudWorkerChannelOptions options) : IWorkerChannelConnector
{
    /// <inheritdoc />
    public async Task<WebSocket> ConnectAsync(Uri uri, string credential, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", "Bearer " + credential);
            socket.Options.KeepAliveInterval = options.KeepAliveInterval;
            socket.Options.KeepAliveTimeout = options.KeepAliveTimeout;
            socket.Options.CollectHttpResponseDetails = true;
            using var connectTimeout = new CancellationTokenSource(options.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                connectTimeout.Token);
            await socket.ConnectAsync(uri, linked.Token).ConfigureAwait(false);
            return socket;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new WorkerChannelUnavailableException("Worker channel connect timed out.");
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException)
        {
            var status = socket.HttpStatusCode;
            socket.Dispose();
            // Messages carry only the status: the underlying exception text can echo request details.
            if (status == HttpStatusCode.Unauthorized)
                throw new WorkerChannelAuthenticationException("The control plane rejected the worker credential.");
            throw new WorkerChannelUnavailableException(
                status == 0
                    ? "Worker channel connect failed (transport error)."
                    : $"Worker channel connect failed (HTTP {(int)status}).");
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
