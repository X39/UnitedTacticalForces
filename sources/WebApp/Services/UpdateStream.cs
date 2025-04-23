using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using X39.Util;

namespace X39.UnitedTacticalForces.WebApp.Services;
// https://sahansera.dev/understanding-websockets-with-aspnetcore-5/
public class UpdateStream : IAsyncDisposable
{
    private readonly ClientWebSocket _websocket;
    private readonly Uri             _websocketUrl;

    public UpdateStream(ClientWebSocket websocket, Uri websocketUrl)
    {
        _websocket    = websocket;
        _websocketUrl = websocketUrl;
    }

    /// <summary>
    /// Connect to the websocket and begin yielding messages
    /// received from the connection.
    /// </summary>
    public async IAsyncEnumerable<string> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_websocket.State is not WebSocketState.Open)
            await _websocket.ConnectAsync(_websocketUrl, cancellationToken)
                .ConfigureAwait(false);
        var buffer = new byte[2048];
        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();
            do
            {
                result = await _websocket.ReceiveAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            ms.Seek(0, SeekOrigin.Begin);

            yield return Encoding.UTF8.GetString(ms.ToArray());

            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }

    /// <summary>
    /// Send a message on the websocket.
    /// This method assumes you've already connected via ConnectAsync
    /// </summary>
    public async Task SendStringAsync(string data, CancellationToken cancellationToken)
    {
        if (_websocket.State is not WebSocketState.Open)
            await _websocket.ConnectAsync(_websocketUrl, cancellationToken)
                .ConfigureAwait(false);
        var encoded = Encoding.UTF8.GetBytes(data);
        await _websocket.SendAsync(encoded, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_websocket.State is WebSocketState.Open)
            await Fault.IgnoreAsync(
                    // ReSharper disable once AccessToDisposedClosure
                    async () => await _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default)
                        .ConfigureAwait(false))
                .ConfigureAwait(false);
        _websocket.Dispose();
    }
}