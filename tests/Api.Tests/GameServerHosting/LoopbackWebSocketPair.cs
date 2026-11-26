using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Creates a connected pair of real <see cref="WebSocket"/> instances over a loopback TCP connection, one for the
/// server end and one for the client end.
/// </summary>
/// <remarks>
/// The pair speaks the real WebSocket framing, masking and close handshake, so channel tests exercise the same
/// receive, fragmentation and close behavior as a production socket without hosting a web server.
/// </remarks>
internal sealed class LoopbackWebSocketPair : IAsyncDisposable
{
    private readonly TcpClient _clientTcp;
    private readonly TcpClient _serverTcp;

    private LoopbackWebSocketPair(TcpClient clientTcp, TcpClient serverTcp, WebSocket client, WebSocket server)
    {
        _clientTcp = clientTcp;
        _serverTcp = serverTcp;
        Client = client;
        Server = server;
    }

    /// <summary>Gets the socket of the connecting end, the end a worker would hold.</summary>
    /// <value>A client-role WebSocket whose outgoing frames are masked.</value>
    public WebSocket Client { get; }

    /// <summary>Gets the socket of the accepting end, the end the control plane would hold.</summary>
    /// <value>A server-role WebSocket.</value>
    public WebSocket Server { get; }

    /// <summary>
    /// Opens a loopback TCP connection and wraps both ends in WebSockets.
    /// </summary>
    /// <returns>The connected pair; dispose it to abort both sockets and release the connection.</returns>
    public static async Task<LoopbackWebSocketPair> CreateAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            var clientTcp = new TcpClient();
            var acceptTask = listener.AcceptTcpClientAsync();
            await clientTcp.ConnectAsync(IPAddress.Loopback, port);
            var serverTcp = await acceptTask;
            var server = WebSocket.CreateFromStream(
                serverTcp.GetStream(),
                new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = Timeout.InfiniteTimeSpan });
            var client = WebSocket.CreateFromStream(
                clientTcp.GetStream(),
                new WebSocketCreationOptions { IsServer = false, KeepAliveInterval = Timeout.InfiniteTimeSpan });
            return new LoopbackWebSocketPair(clientTcp, serverTcp, client, server);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Aborts both sockets and closes the TCP connection.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Server.Dispose();
        _clientTcp.Dispose();
        _serverTcp.Dispose();
        return ValueTask.CompletedTask;
    }
}
