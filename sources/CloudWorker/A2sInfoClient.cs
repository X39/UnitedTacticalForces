using System.Net;
using System.Net.Sockets;
using System.Text;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Queries A2S_INFO over UDP on the loopback interface only. The worker opens no listener: each query uses one
/// short-lived socket bound to an ephemeral loopback port.
/// </summary>
public sealed class A2sInfoClient : IA2sInfoClient
{
    /// <summary>The most requests one query sends, which bounds a server that keeps answering challenges.</summary>
    public const int MaxRounds = 3;

    private const byte InfoRequestType = 0x54;
    private const byte ChallengeReplyType = 0x41;
    private const byte InfoReplyType = 0x49;
    private const int HeaderLength = 5;
    private const int ChallengeLength = 4;

    /// <inheritdoc />
    public async Task<A2sInfo?> QueryAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        try
        {
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            client.Connect(IPAddress.Loopback, port);
            byte[]? challenge = null;
            for (var round = 0; round < MaxRounds; round++)
            {
                await client.SendAsync(BuildRequest(challenge), bounded.Token).ConfigureAwait(false);
                var reply = (await client.ReceiveAsync(bounded.Token).ConfigureAwait(false)).Buffer;
                if (reply.Length < HeaderLength || !HasSinglePacketHeader(reply)) return null;
                switch (reply[4])
                {
                    case ChallengeReplyType:
                        if (reply.Length < HeaderLength + ChallengeLength) return null;
                        challenge = reply[HeaderLength..(HeaderLength + ChallengeLength)];
                        continue;
                    case InfoReplyType:
                        return ParseInfo(reply);
                    default:
                        return null;
                }
            }
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static bool HasSinglePacketHeader(byte[] reply)
        => reply[0] == 0xFF && reply[1] == 0xFF && reply[2] == 0xFF && reply[3] == 0xFF;

    private static byte[] BuildRequest(byte[]? challenge)
    {
        var text = Encoding.ASCII.GetBytes("Source Engine Query\0");
        var request = new byte[HeaderLength + text.Length + (challenge?.Length ?? 0)];
        request.AsSpan(0, 4).Fill(0xFF);
        request[4] = InfoRequestType;
        text.CopyTo(request, HeaderLength);
        challenge?.CopyTo(request, HeaderLength + text.Length);
        return request;
    }

    private static A2sInfo? ParseInfo(byte[] reply)
    {
        // Layout after the header: protocol (byte), name, map, folder, game (NUL strings), app id (int16),
        // players, max players and bots (one byte each).
        var position = HeaderLength + 1;
        for (var field = 0; field < 4; field++)
        {
            var terminator = Array.IndexOf(reply, (byte)0, Math.Min(position, reply.Length));
            if (terminator < 0) return null;
            position = terminator + 1;
        }
        position += 2;
        if (position + 3 > reply.Length) return null;
        return new A2sInfo(reply[position], reply[position + 1], reply[position + 2]);
    }
}
