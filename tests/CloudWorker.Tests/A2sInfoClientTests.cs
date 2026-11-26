using System.Net;
using System.Net.Sockets;
using System.Text;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Verifies the loopback A2S_INFO client against an in-process UDP responder.</summary>
public sealed class A2sInfoClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(400);

    /// <summary>Verifies that a direct info reply is decoded and humans exclude bots.</summary>
    /// <returns>A task that completes after the query.</returns>
    [Fact]
    public async Task QueryAsync_DirectInfoReply_ReturnsPlayersCapacityAndBots()
    {
        await using var responder = new UdpResponder(_ => InfoReply(players: 7, maxPlayers: 16, bots: 2));

        var info = await new A2sInfoClient().QueryAsync(responder.Port, Timeout, CancellationToken.None);

        Assert.Equal(new A2sInfo(7, 16, 2), info);
        Assert.Equal(5, info!.Humans);
        Assert.Single(responder.Requests);
        Assert.Equal(RequestPrefix(), responder.Requests[0]);
    }

    /// <summary>Verifies that a challenge reply is answered by appending the challenge to the request.</summary>
    /// <returns>A task that completes after the query.</returns>
    [Fact]
    public async Task QueryAsync_ChallengeThenInfo_AppendsChallengeInLittleEndianOrder()
    {
        const int challenge = 0x11223344;
        await using var responder = new UdpResponder(request => request.Length > RequestPrefix().Length
            ? InfoReply(players: 3, maxPlayers: 10, bots: 0)
            : ChallengeReply(challenge));

        var info = await new A2sInfoClient().QueryAsync(responder.Port, Timeout, CancellationToken.None);

        Assert.Equal(new A2sInfo(3, 10, 0), info);
        Assert.Equal(2, responder.Requests.Count);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, responder.Requests[1][^4..]);
        Assert.Equal(RequestPrefix(), responder.Requests[1][..^4]);
    }

    /// <summary>Verifies that a silent server yields null once the timeout elapses.</summary>
    /// <returns>A task that completes after the query.</returns>
    [Fact]
    public async Task QueryAsync_NoReply_ReturnsNullWithinTimeout()
    {
        await using var responder = new UdpResponder(_ => null);
        var started = DateTime.UtcNow;

        var info = await new A2sInfoClient().QueryAsync(responder.Port, Timeout, CancellationToken.None);

        Assert.Null(info);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    /// <summary>Verifies that split packets and truncated replies are rejected as malformed.</summary>
    /// <param name="kind">Which malformed reply the responder sends.</param>
    /// <returns>A task that completes after the query.</returns>
    [Theory]
    [InlineData("split")]
    [InlineData("truncated")]
    [InlineData("empty")]
    [InlineData("wrong-type")]
    public async Task QueryAsync_MalformedReply_ReturnsNull(string kind)
    {
        await using var responder = new UdpResponder(_ => kind switch
        {
            "split" => [0xFF, 0xFF, 0xFF, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            "truncated" => TruncatedInfoReply(),
            "empty" => [],
            _ => [0xFF, 0xFF, 0xFF, 0xFF, 0x7A, 0x01, 0x02, 0x03],
        });

        var info = await new A2sInfoClient().QueryAsync(responder.Port, Timeout, CancellationToken.None);

        Assert.Null(info);
    }

    /// <summary>Verifies that an endless challenge loop stops after three rounds.</summary>
    /// <returns>A task that completes after the query.</returns>
    [Fact]
    public async Task QueryAsync_EndlessChallenges_StopsAfterThreeRoundsWithNull()
    {
        await using var responder = new UdpResponder(_ => ChallengeReply(0x01020304));

        var info = await new A2sInfoClient().QueryAsync(responder.Port, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Null(info);
        Assert.Equal(3, responder.Requests.Count);
    }

    /// <summary>Verifies that bots above players never produce a negative human count.</summary>
    [Fact]
    public void Humans_BotsAbovePlayers_IsZero()
    {
        Assert.Equal(0, new A2sInfo(2, 10, 5).Humans);
    }

    /// <summary>Verifies that caller cancellation is the only way the client throws.</summary>
    /// <returns>A task that completes after the query.</returns>
    [Fact]
    public async Task QueryAsync_CallerCancelled_Throws()
    {
        await using var responder = new UdpResponder(_ => null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new A2sInfoClient().QueryAsync(responder.Port, TimeSpan.FromSeconds(5), cancellation.Token));
    }

    private static byte[] RequestPrefix()
    {
        var text = Encoding.ASCII.GetBytes("Source Engine Query\0");
        return [0xFF, 0xFF, 0xFF, 0xFF, 0x54, .. text];
    }

    private static byte[] ChallengeReply(int challenge)
    {
        var reply = new byte[9];
        reply[0] = reply[1] = reply[2] = reply[3] = 0xFF;
        reply[4] = 0x41;
        BitConverter.GetBytes(challenge).CopyTo(reply, 5);
        return reply;
    }

    private static byte[] TruncatedInfoReply()
    {
        var bytes = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0x49, 17 };
        foreach (var text in new[] { "Server", "map", "folder", "game" })
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(text));
            bytes.Add(0);
        }
        bytes.AddRange([0x01, 0x00, 0x05]);
        return [.. bytes];
    }

    private static byte[] InfoReply(byte players, byte maxPlayers, byte bots)
    {
        var bytes = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0x49, 17 };
        foreach (var text in new[] { "Server", "map", "folder", "game" })
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(text));
            bytes.Add(0);
        }
        bytes.AddRange([0x01, 0x00, players, maxPlayers, bots, (byte)'d', (byte)'l', 0, 1]);
        bytes.AddRange(Encoding.UTF8.GetBytes("1.0.0"));
        bytes.Add(0);
        return [.. bytes];
    }

    private sealed class UdpResponder : IAsyncDisposable
    {
        private readonly UdpClient _socket = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;

        public UdpResponder(Func<byte[], byte[]?> handler)
        {
            _loop = Task.Run(async () =>
            {
                try
                {
                    while (!_stop.IsCancellationRequested)
                    {
                        var received = await _socket.ReceiveAsync(_stop.Token);
                        lock (Requests) Requests.Add(received.Buffer);
                        var reply = handler(received.Buffer);
                        if (reply is not null)
                            await _socket.SendAsync(reply, received.RemoteEndPoint, _stop.Token);
                    }
                }
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException
                                                      or SocketException)
                {
                    // The responder stops when the test disposes it.
                }
            });
        }

        public List<byte[]> Requests { get; } = [];

        public int Port => ((IPEndPoint)_socket.Client.LocalEndPoint!).Port;

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _socket.Dispose();
            await _loop;
            _stop.Dispose();
        }
    }
}
