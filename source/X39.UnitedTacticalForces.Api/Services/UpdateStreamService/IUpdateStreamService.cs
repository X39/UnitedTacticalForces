using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.Util.Collections;
using X39.Util.Collections.Concurrent;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;

namespace X39.UnitedTacticalForces.Api.Services;

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
    private readonly Dictionary<string, List<WebSocket>> _webSockets = new();
    private readonly ReaderWriterLockSlim                _lock       = new();

    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        WriteIndented    = false,
        MaxDepth         = 64,
    };

    /// <inheritdoc />
    public ValueTask RegisterAsync(string path, WebSocket webSocket)
    {
        _lock.WriteLocked(() => _webSockets.GetOrAdd(path, () => new List<WebSocket>()).Add(webSocket));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask SendUpdateAsync<T>(string path, T package)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(package, _serializerOptions);
        var webSockets = _lock.ReadLocked(
            () => !_webSockets.TryGetValue(path, out var webSockets)
                ? Array.Empty<WebSocket>()
                : webSockets.ToArray());
        foreach (var webSocket in webSockets)
        {
            await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var webSockets = _lock.WriteLocked(
            () =>
            {
                var webSockets = _webSockets.SelectMany((q) => q.Value).Distinct().ToArray();
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