using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Creates a connected pair of real <see cref="WebSocket"/> instances over a loopback TCP connection, one for the
/// server end and one for the client end, so the UAT worker emulation can drive the real worker channel handler.
/// </summary>
/// <remarks>
/// The pair speaks the real WebSocket framing, masking and close handshake, exactly like a production socket, without a
/// web server in between. It only ever binds <see cref="IPAddress.Loopback"/> on an ephemeral port and is used only by
/// the Development-only UAT emulation.
/// </remarks>
internal sealed class UatLoopbackWebSocketPair : IAsyncDisposable
{
    private readonly TcpClient _clientTcp;
    private readonly TcpClient _serverTcp;

    /// <summary>
    /// Wraps the two connected ends.
    /// </summary>
    /// <param name="clientTcp">The connecting TCP end.</param>
    /// <param name="serverTcp">The accepted TCP end.</param>
    /// <param name="client">The client-role socket over <paramref name="clientTcp"/>.</param>
    /// <param name="server">The server-role socket over <paramref name="serverTcp"/>.</param>
    private UatLoopbackWebSocketPair(TcpClient clientTcp, TcpClient serverTcp, WebSocket client, WebSocket server)
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
    /// <param name="cancellationToken">A token that cancels connecting.</param>
    /// <returns>The connected pair; dispose it to abort both sockets and release the connection.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled before the connection was made.</exception>
    public static async Task<UatLoopbackWebSocketPair> CreateAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            var clientTcp = new TcpClient();
            var acceptTask = listener.AcceptTcpClientAsync(cancellationToken).AsTask();
            await clientTcp.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
            var serverTcp = await acceptTask.ConfigureAwait(false);
            var server = WebSocket.CreateFromStream(
                serverTcp.GetStream(),
                new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = Timeout.InfiniteTimeSpan });
            var client = WebSocket.CreateFromStream(
                clientTcp.GetStream(),
                new WebSocketCreationOptions { IsServer = false, KeepAliveInterval = Timeout.InfiniteTimeSpan });
            return new UatLoopbackWebSocketPair(clientTcp, serverTcp, client, server);
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
