using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class CounterStrike2RconClient : ICounterStrike2RconClient
{
    public async Task<bool> RequestQuitAsync(
        int port,
        string password,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, port, bounded.Token).ConfigureAwait(false);
        await using var stream = client.GetStream();
        const int requestId = 0x555446;
        await WritePacketAsync(stream, requestId, 3, password, bounded.Token).ConfigureAwait(false);
        var authenticated = await ReadPacketAsync(stream, bounded.Token).ConfigureAwait(false);
        if (authenticated.Type != 2)
            authenticated = await ReadPacketAsync(stream, bounded.Token).ConfigureAwait(false);
        if (authenticated.RequestId != requestId || authenticated.Type != 2) return false;
        await WritePacketAsync(stream, requestId, 2, "quit", bounded.Token).ConfigureAwait(false);
        return true;
    }

    /// <summary>The longest pause between response packets that still counts as part of the same reply.</summary>
    private static readonly TimeSpan ReadPause = TimeSpan.FromMilliseconds(250);

    /// <summary>The most response bytes a status request collects before it stops reading.</summary>
    private const int ResponseCapBytes = 64 * 1024;

    /// <inheritdoc />
    public async Task<string?> RequestStatusAsync(
        int port,
        string password,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, port, bounded.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            const int authenticationId = 0x555446;
            const int statusId = 0x555447;
            await WritePacketAsync(stream, authenticationId, 3, password, bounded.Token).ConfigureAwait(false);
            var authenticated = await ReadPacketAsync(stream, bounded.Token).ConfigureAwait(false);
            if (authenticated.Type != 2)
                authenticated = await ReadPacketAsync(stream, bounded.Token).ConfigureAwait(false);
            if (authenticated.RequestId != authenticationId || authenticated.Type != 2) return null;
            await WritePacketAsync(stream, statusId, 2, "status", bounded.Token).ConfigureAwait(false);
            var text = new StringBuilder();
            var total = 0;
            while (total < ResponseCapBytes)
            {
                using var pause = CancellationTokenSource.CreateLinkedTokenSource(bounded.Token);
                // The first chunk may take as long as the overall timeout; later chunks must follow quickly.
                if (total > 0) pause.CancelAfter(ReadPause);
                try
                {
                    var packet = await ReadBodyPacketAsync(stream, pause.Token).ConfigureAwait(false);
                    if (packet.RequestId != statusId || packet.Type != 0) continue;
                    total += packet.Body.Length;
                    text.Append(Encoding.UTF8.GetString(packet.Body));
                }
                catch (OperationCanceledException) when (total > 0
                                                         && !bounded.IsCancellationRequested
                                                         && !cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            return total > 0 ? text.ToString() : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (exception is SocketException or IOException or InvalidDataException
                                              or EndOfStreamException or ObjectDisposedException)
        {
            return null;
        }
    }

    private static async Task WritePacketAsync(Stream stream, int requestId, int type, string body, CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetBytes(body);
        var packet = new byte[14 + text.Length];
        BinaryPrimitives.WriteInt32LittleEndian(packet, packet.Length - 4);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), type);
        text.CopyTo(packet.AsSpan(12));
        await stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int RequestId, int Type)> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        var packet = await ReadBodyPacketAsync(stream, cancellationToken).ConfigureAwait(false);
        return (packet.RequestId, packet.Type);
    }

    /// <summary>Reads one RCON packet including its body, bounded to the 4096 byte packet limit.</summary>
    /// <param name="stream">The authenticated RCON stream.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The request id, packet type and the body bytes without the two terminating NUL bytes.</returns>
    /// <exception cref="InvalidDataException">The announced packet length is outside 10 to 4096.</exception>
    private static async Task<(int RequestId, int Type, byte[] Body)> ReadBodyPacketAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];
        await stream.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (length is < 10 or > 4096) throw new InvalidDataException("CS2 RCON response length is invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4)),
            payload[8..(length - 2)]);
    }
}
