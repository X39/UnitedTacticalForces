using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using X39.Util;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;
using X39.UnitedTacticalForces.WebSockets.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.UpdateStreamService;

/// <summary>
/// Keeps the registered update-stream WebSockets of this process and sends typed updates to the sockets whose path
/// starts with the addressed path.
/// </summary>
/// <remarks>
/// A WebSocket allows only one outstanding send. Several producers write to the same per-user paths, for example
/// the fleet change notifier and the cost alert publisher, so every socket carries its own send lock and sends to
/// one socket are serialized. Without it, the second of two simultaneous sends would throw and the socket would be
/// removed as if it had failed. Sends to different sockets do not wait for each other.
/// </remarks>
[Singleton<UpdateStreamService, IUpdateStreamService>]
public class UpdateStreamService : IUpdateStreamService, IAsyncDisposable
{
    /// <summary>
    /// The registered subscriptions: the registered path, the socket and the lock that serializes sends to it.
    /// </summary>
    /// <remarks>
    /// The send locks are intentionally never disposed when a socket is removed: a sender that is still queued on
    /// one may release it afterwards, and a <see cref="SemaphoreSlim"/> whose wait handle was never accessed holds no
    /// unmanaged resource, so leaving it to the garbage collector is safe.
    /// </remarks>
    private readonly List<(string path, WebSocket webSocket, SemaphoreSlim sendLock)> _webSockets = [];

    /// <summary>
    /// Guards <see cref="_webSockets"/>.
    /// </summary>
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// The serializer settings of every frame: web defaults with reference preservation and no custom converters.
    /// </summary>
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented    = false,
        MaxDepth         = 64,
    };

    /// <inheritdoc />
    public async ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
    {
        _lock.WriteLocked(() => _webSockets.Add((path, webSocket, new SemaphoreSlim(1, 1))));
        var buffer = new byte[1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && webSocket.State is WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType is not WebSocketMessageType.Close)
                    continue;

                if (webSocket.State is WebSocketState.CloseReceived)
                {
                    await webSocket.CloseAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            result.CloseStatusDescription,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }

                break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            _lock.WriteLocked(() => _webSockets.RemoveAll(q => q.webSocket == webSocket));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Each socket is sent to under its own send lock, so concurrent calls for one path deliver all frames in turn
    /// instead of failing. A socket whose send throws is removed, as before.
    /// </remarks>
    public async ValueTask SendUpdateAsync<T>(string path, T package)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new WebSocketMessage<T> {Value = package},
            _serializerOptions);
        var webSockets = _lock.ReadLocked(() => _webSockets.Where(q => q.path.StartsWith(path)).ToArray());
        var webSocketsToRemove = new List<WebSocket>();
        foreach (var (_, webSocket, sendLock) in webSockets)
        {
            await sendLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)
                               .ConfigureAwait(false);
            }
            catch
            {
                webSocketsToRemove.Add(webSocket);
            }
            finally
            {
                sendLock.Release();
            }
        }

        if (webSocketsToRemove.Any())
            _lock.WriteLocked(() => _webSockets.RemoveAll(q => webSocketsToRemove.Contains(q.webSocket)));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var webSockets = _lock.WriteLocked(
            () =>
            {
                var webSockets = _webSockets.Select(q => q.webSocket).ToArray();
                _webSockets.Clear();
                return webSockets;
            });
        foreach (var webSocket in webSockets)
        {
            await webSocket.CloseAsync(
                               WebSocketCloseStatus.EndpointUnavailable,
                               "Server is shutting down",
                               CancellationToken.None)
                           .ConfigureAwait(false);
            webSocket.Dispose();
        }

        _lock.Dispose();
    }
}
