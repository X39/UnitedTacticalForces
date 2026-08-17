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
        var lengthBytes = new byte[4];
        await stream.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (length is < 10 or > 4096) throw new InvalidDataException("CS2 RCON response length is invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return (BinaryPrimitives.ReadInt32LittleEndian(payload), BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4)));
    }
}
