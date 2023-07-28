using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using X39.Util;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;

namespace X39.UnitedTacticalForces.Api.Services.UpdateStreamService;

public interface IUpdateStreamService
{
    /// <summary>
    /// Registers a websocket for a user.
    /// </summary>
    /// <param name="path">The update path to subscribe to.</param>
    /// <param name="webSocket">The websocket to communicate the changes to.</param>
    /// <returns>An awaitable <see cref="ValueTask"/> that completes once the websocket was added.</returns>
    ValueTask RegisterAsync(string path, WebSocket webSocket);

    /// <summary>
    /// Allows to send an update to all registered websockets for a specific path.
    /// </summary>
    /// <param name="path">The path to send the update to.</param>
    /// <param name="package">The data to send.</param>
    /// <typeparam name="T">The type of <paramref name="package"/>.</typeparam>
    /// <returns>An awaitable task that completes once the update was send to all participants.</returns>
    ValueTask SendUpdateAsync<T>(string path, T package);
}

[Singleton<UpdateStreamService, IUpdateStreamService>]
public class UpdateStreamService : IUpdateStreamService, IAsyncDisposable
{
    private class Wrapper<T>
    {
        public string Type { get; init; } = typeof(T).FullName();
        public T Value { get; init; }
    }

    private readonly List<(string path, WebSocket webSocket)> _webSockets = new();
    private readonly ReaderWriterLockSlim                     _lock       = new();

    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented    = false,
        MaxDepth         = 64,
    };

    /// <inheritdoc />
    public ValueTask RegisterAsync(string path, WebSocket webSocket)
    {
        _lock.WriteLocked(() => _webSockets.Add((path, webSocket)));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask SendUpdateAsync<T>(string path, T package)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Wrapper<T> {Value = package}, _serializerOptions);
        var webSockets = _lock.ReadLocked(() => _webSockets.Where((q) => q.path.StartsWith(path)).ToArray());
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
            _lock.WriteLocked(() => _webSockets.RemoveAll((q) => webSocketsToRemove.Contains(q.webSocket)));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var webSockets = _lock.WriteLocked(
            () =>
            {
                var webSockets = _webSockets.Select((q) => q.webSocket).ToArray();
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