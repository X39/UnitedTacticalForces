using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using X39.Util;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;
using X39.UnitedTacticalForces.WebSockets.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.UpdateStreamService;

[Singleton<UpdateStreamService, IUpdateStreamService>]
public class UpdateStreamService : IUpdateStreamService, IAsyncDisposable
{
    private readonly List<(string path, WebSocket webSocket)> _webSockets = [];
    private readonly ReaderWriterLockSlim                     _lock       = new();

    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented    = false,
        MaxDepth         = 64,
    };

    /// <inheritdoc />
    public async ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
    {
        _lock.WriteLocked(() => _webSockets.Add((path, webSocket)));
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
    public async ValueTask SendUpdateAsync<T>(string path, T package)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new WebSocketMessage<T> {Value = package},
            _serializerOptions);
        var webSockets = _lock.ReadLocked(() => _webSockets.Where(q => q.path.StartsWith(path)).ToArray());
        var webSocketsToRemove = new List<WebSocket>();
        foreach (var (_, webSocket) in webSockets)
        {
            try
            {
                await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)
                               .ConfigureAwait(false);
            }
            catch
            {
                webSocketsToRemove.Add(webSocket);
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
