using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace CloudWorker.Tests;

/// <summary>
/// Two connected WebSocket ends over a real loopback TCP connection, built with
/// <see cref="WebSocket.CreateFromStream(Stream, WebSocketCreationOptions)"/> so no package or web host is needed.
/// </summary>
internal sealed class LoopbackWebSocketPair : IAsyncDisposable
{
    private LoopbackWebSocketPair(WebSocket client, WebSocket server)
    {
        Client = client;
        Server = server;
    }

    /// <summary>Gets the client end that is handed to the code under test.</summary>
    /// <value>The client-side WebSocket.</value>
    public WebSocket Client { get; }

    /// <summary>Gets the server end that the test drives.</summary>
    /// <value>The server-side WebSocket.</value>
    public WebSocket Server { get; }

    /// <summary>
    /// Opens a loopback TCP connection and wraps both ends in WebSockets.
    /// </summary>
    /// <param name="wrapClientStream">
    /// Optional decorator for the client stream, used to stall writes in send-timeout tests.
    /// </param>
    /// <returns>The connected pair.</returns>
    public static async Task<LoopbackWebSocketPair> CreateAsync(Func<Stream, Stream>? wrapClientStream = null)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var clientTcp = new TcpClient();
        var accept = listener.AcceptTcpClientAsync();
        await clientTcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        var serverTcp = await accept.ConfigureAwait(false);
        Stream clientStream = clientTcp.GetStream();
        if (wrapClientStream is not null)
            clientStream = wrapClientStream(clientStream);
        var client = WebSocket.CreateFromStream(
            clientStream,
            new WebSocketCreationOptions { IsServer = false, KeepAliveInterval = TimeSpan.Zero });
        var server = WebSocket.CreateFromStream(
            serverTcp.GetStream(),
            new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
        return new LoopbackWebSocketPair(client, server);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Server.Dispose();
        return ValueTask.CompletedTask;
    }
}
