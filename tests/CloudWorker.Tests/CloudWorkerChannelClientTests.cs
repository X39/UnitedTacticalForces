using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>
/// Tests the worker channel client over real loopback sockets: correlated requests and acks, wake latching,
/// reconnect, credential rotation, authentication failure, size and time limits, and secret-free diagnostics.
/// </summary>
public sealed class CloudWorkerChannelClientTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>Builds the channel URI for https and http base URLs.</summary>
    /// <param name="baseUrl">The API base URL.</param>
    /// <param name="scheme">The expected WebSocket scheme.</param>
    [Theory]
    [InlineData("https://api.example/api/", "wss")]
    [InlineData("http://api.example/api", "ws")]
    public void BuildChannelUri_MapsSchemeAndKeepsCredentialOutOfTheUri(string baseUrl, string scheme)
    {
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap() with { ApiBaseUrl = baseUrl };

        var uri = CloudWorkerChannelClient.BuildChannelUri(connection);

        Assert.Equal(
            $"{scheme}://api.example/api/lifecycle-workers/{connection.WorkerId:D}/channel"
            + $"?deploymentId={connection.DeploymentId:D}",
            uri.ToString());
    }

    /// <summary>The heartbeat frame carries a fresh id and completes only with its ack.</summary>
    /// <returns>A task that completes after the exchange is checked.</returns>
    [Fact]
    public async Task Heartbeat_SendsCorrelatedFrameAndCompletesWithItsAck()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        await using var channel = await StartAsync(server, connection, new CredentialSource("cred-1"));

        var heartbeat = channel.HeartbeatAsync(
            new WorkerHeartbeatRequest { DeploymentId = connection.DeploymentId, Stage = "idle" },
            CancellationToken.None);
        var serverConnection = await server.WaitForConnectionAsync(0);
        var frame = await serverConnection.ReadFrameAsync();

        Assert.Equal("cred-1", serverConnection.Credential);
        Assert.Equal(EWorkerChannelFrameType.Heartbeat, frame.Type);
        Assert.NotNull(frame.Id);
        Assert.NotEqual(Guid.Empty, frame.Id);
        Assert.Equal(connection.DeploymentId, frame.Payload!.Value.GetProperty("deploymentId").GetGuid());
        Assert.False(heartbeat.IsCompleted);

        await serverConnection.AckAsync(frame);
        await heartbeat.WaitAsync(Limit);
    }

    /// <summary>A claim returns the command from an accepted ack and null for a JSON null payload.</summary>
    /// <returns>A task that completes after both claims are checked.</returns>
    [Fact]
    public async Task Claim_ReturnsCommandFromAckPayloadOrNull()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            connection.WorkerId,
            connection.DeploymentId,
            connection.ServerId);
        await using var channel = await StartAsync(server, connection, new CredentialSource("cred-1"));
        var serverConnection = await server.WaitForConnectionAsync(0);

        var claim = channel.ClaimNextAsync(CancellationToken.None);
        var frame = await serverConnection.ReadFrameAsync();
        Assert.Equal(EWorkerChannelFrameType.Claim, frame.Type);
        Assert.Equal(connection.DeploymentId, frame.Payload!.Value.GetProperty("deploymentId").GetGuid());
        await serverConnection.AckAsync(frame, payload: command);
        var claimed = await claim.WaitAsync(Limit);

        var empty = channel.ClaimNextAsync(CancellationToken.None);
        await serverConnection.AckAsync(await serverConnection.ReadFrameAsync());

        Assert.NotNull(claimed);
        Assert.Equal(command.CommandId, claimed.CommandId);
        Assert.Equal(command.ActionIdentifier, claimed.ActionIdentifier);
        Assert.Null(await empty.WaitAsync(Limit));
    }

    /// <summary>A succeeded report maps to the process body; Superseded acks return false like HTTP 409.</summary>
    /// <returns>A task that completes after both reports are checked.</returns>
    [Fact]
    public async Task Report_SendsProcessBodyAndMapsSupersessionToFalse()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        await using var channel = await StartAsync(server, connection, new CredentialSource("cred-1"));
        var serverConnection = await server.WaitForConnectionAsync(0);
        var commandId = Guid.NewGuid();
        var report = new WorkerCommandReportRequest(
            connection.DeploymentId,
            EWorkerCommandReportState.Succeeded,
            "started",
            DateTimeOffset.UtcNow,
            null,
            null,
            "done",
            42,
            null);

        var accepted = channel.ReportCommandAsync(commandId, report, CancellationToken.None);
        var frame = await serverConnection.ReadFrameAsync();
        await serverConnection.AckAsync(frame);
        var acceptedResult = await accepted.WaitAsync(Limit);

        var superseded = channel.ReportCommandAsync(commandId, report, CancellationToken.None);
        await serverConnection.AckAsync(
            await serverConnection.ReadFrameAsync(),
            EWorkerChannelOutcome.Superseded);
        var supersededResult = await superseded.WaitAsync(Limit);

        Assert.Equal(EWorkerChannelFrameType.ReportSucceeded, frame.Type);
        Assert.Equal(commandId, frame.CommandId);
        Assert.Equal("started", frame.Payload!.Value.GetProperty("stage").GetString());
        Assert.Equal(42, frame.Payload!.Value.GetProperty("processId").GetInt32());
        Assert.Equal("done", frame.Payload!.Value.GetProperty("output").GetString());
        Assert.True(acceptedResult);
        Assert.False(supersededResult);
    }

    /// <summary>Two requests whose acks arrive in reverse order each complete with their own result.</summary>
    /// <returns>A task that completes after both results are checked.</returns>
    [Fact]
    public async Task ConcurrentRequests_CompleteWithTheirOwnAckWhenAcksArriveReversed()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            connection.WorkerId,
            connection.DeploymentId,
            connection.ServerId);
        await using var channel = await StartAsync(server, connection, new CredentialSource("cred-1"));
        var serverConnection = await server.WaitForConnectionAsync(0);

        var claim = channel.ClaimNextAsync(CancellationToken.None);
        var claimFrame = await serverConnection.ReadFrameAsync();
        var heartbeat = channel.HeartbeatAsync(
            new WorkerHeartbeatRequest { DeploymentId = connection.DeploymentId },
            CancellationToken.None);
        var heartbeatFrame = await serverConnection.ReadFrameAsync();

        await serverConnection.AckAsync(heartbeatFrame);
        await heartbeat.WaitAsync(Limit);
        Assert.False(claim.IsCompleted);
        await serverConnection.AckAsync(claimFrame, payload: command);
        var claimed = await claim.WaitAsync(Limit);

        Assert.Equal(EWorkerChannelFrameType.Claim, claimFrame.Type);
        Assert.Equal(EWorkerChannelFrameType.Heartbeat, heartbeatFrame.Type);
        Assert.NotEqual(claimFrame.Id, heartbeatFrame.Id);
        Assert.Equal(command.CommandId, claimed!.CommandId);
    }

    /// <summary>A wake pushed before the wait is remembered once; a second wait stays pending.</summary>
    /// <returns>A task that completes after the latch behavior is checked.</returns>
    [Fact]
    public async Task Wake_IsLatchedAndConsumedOnce()
    {
        var server = new FakeWorkerChannelServer();
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"));
        var serverConnection = await server.WaitForConnectionAsync(0);
        await channel.WaitForWakeAsync(CancellationToken.None).WaitAsync(Limit);

        await serverConnection.SendWakeAsync();
        await serverConnection.SendWakeAsync();
        await Task.Delay(150);
        await channel.WaitForWakeAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        var second = channel.WaitForWakeAsync(CancellationToken.None);
        await Task.Delay(200);

        Assert.False(second.IsCompleted);
        await serverConnection.SendWakeAsync();
        await second.WaitAsync(Limit);
    }

    /// <summary>The shared mapper keeps the HTTP bodies and frame types stable, including failure defaults.</summary>
    [Fact]
    public void ReportMapper_ChoosesBodyAndFrameTypePerState()
    {
        var deploymentId = Guid.NewGuid();
        WorkerCommandReportRequest Report(EWorkerCommandReportState state)
            => new(deploymentId, state, "stage", DateTimeOffset.UtcNow, null, null, null, null, null);

        Assert.IsType<WorkerCommandStageRequest>(
            WorkerCommandReportMapper.ToContract(Report(EWorkerCommandReportState.Received)));
        Assert.IsType<WorkerCommandProcessRequest>(
            WorkerCommandReportMapper.ToContract(Report(EWorkerCommandReportState.Running)));
        var failure = Assert.IsType<WorkerCommandFailureRequest>(
            WorkerCommandReportMapper.ToContract(Report(EWorkerCommandReportState.Failed)));
        Assert.Equal("worker-command-failed", failure.ErrorCode);
        Assert.Equal("Worker command failed.", failure.ErrorMessage);
        Assert.Equal(
            EWorkerChannelFrameType.ReportReceived,
            WorkerCommandReportMapper.ToFrameType(EWorkerCommandReportState.Received));
        Assert.Equal(
            EWorkerChannelFrameType.ReportFailed,
            WorkerCommandReportMapper.ToFrameType(EWorkerCommandReportState.Failed));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorkerCommandReportMapper.ToFrameType((EWorkerCommandReportState)99));
    }

    /// <summary>A dropped socket fails the pending request, reconnects with the current credential and re-wakes.</summary>
    /// <returns>A task that completes after the reconnect is checked.</returns>
    [Fact]
    public async Task Drop_FailsPendingRequestReconnectsWithCurrentCredentialAndLatchesWake()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var credential = new CredentialSource("cred-1");
        await using var channel = await StartAsync(server, connection, credential);
        var first = await server.WaitForConnectionAsync(0);
        await channel.WaitForWakeAsync(CancellationToken.None).WaitAsync(Limit);

        var heartbeat = channel.HeartbeatAsync(
            new WorkerHeartbeatRequest { DeploymentId = connection.DeploymentId },
            CancellationToken.None);
        await first.ReadFrameAsync();
        credential.Value = "cred-2";
        await first.CloseAsync(WebSocketCloseStatus.EndpointUnavailable);

        await Assert.ThrowsAsync<WorkerChannelUnavailableException>(() => heartbeat);
        var second = await server.WaitForConnectionAsync(1);
        await channel.WaitForWakeAsync(CancellationToken.None).WaitAsync(Limit);
        Assert.Equal("cred-2", second.Credential);
    }

    /// <summary>Repeated connect failures back off 1, 2, 4, 8 and then 15 seconds.</summary>
    /// <returns>A task that completes after the delays are checked.</returns>
    [Fact]
    public async Task ConnectFailures_BackOffUpToTheMaximum()
    {
        var time = new ManualTimeProvider();
        var server = new FakeWorkerChannelServer();
        server.FailNextConnects(6);
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"),
            ManualTimeOptions(),
            time);

        foreach (var seconds in new[] { 1, 2, 4, 8, 15, 15 })
        {
            var delay = TimeSpan.FromSeconds(seconds);
            await time.WaitForTimerAsync(delay);
            time.Advance(delay);
        }

        await server.WaitForConnectionAsync(0);
        Assert.Equal(7, server.ConnectAttempts);
    }

    /// <summary>A successful connection resets the backoff to one second.</summary>
    /// <returns>A task that completes after the reset is checked.</returns>
    [Fact]
    public async Task SuccessfulConnection_ResetsBackoff()
    {
        var time = new ManualTimeProvider();
        var server = new FakeWorkerChannelServer();
        server.FailNextConnects(2);
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"),
            ManualTimeOptions(),
            time);
        await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(1));
        await time.WaitForTimerAsync(TimeSpan.FromSeconds(2));
        time.Advance(TimeSpan.FromSeconds(2));
        var first = await server.WaitForConnectionAsync(0);

        await first.CloseAsync(WebSocketCloseStatus.NormalClosure);
        await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(1));

        await server.WaitForConnectionAsync(1);
    }

    /// <summary>A 401 with an unchanged credential faults the client instead of retrying forever.</summary>
    /// <returns>A task that completes after the fault is checked.</returns>
    [Fact]
    public async Task Unauthorized_WithUnchangedCredential_FaultsCompletion()
    {
        var server = new FakeWorkerChannelServer();
        server.RefuseNextConnects(5);
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"));

        await Assert.ThrowsAsync<WorkerChannelAuthenticationException>(() => channel.Completion.WaitAsync(Limit));
        await Assert.ThrowsAsync<WorkerChannelAuthenticationException>(
            () => channel.WaitUntilConnectedAsync(CancellationToken.None));
        Assert.Equal(1, server.ConnectAttempts);
    }

    /// <summary>A 401 followed by a changed credential reconnects with the new credential.</summary>
    /// <returns>A task that completes after the reconnect is checked.</returns>
    [Fact]
    public async Task Unauthorized_FollowedByChangedCredential_Reconnects()
    {
        var server = new FakeWorkerChannelServer();
        server.RefuseNextConnects(1);
        var credential = new CredentialSource("old");
        var provider = new Func<CancellationToken, Task<string>>(_ =>
        {
            var current = credential.Value;
            credential.Value = "new";
            return Task.FromResult(current);
        });
        await using var channel = new CloudWorkerChannelClient(server, FastOptions());
        await channel.StartAsync(CloudWorkerBootstrapConfigTests.CreateBootstrap(), provider, CancellationToken.None);

        var connection = await server.WaitForConnectionAsync(0);

        Assert.Equal("new", connection.Credential);
        Assert.Equal(["old", "new"], server.Credentials);
        Assert.False(channel.Completion.IsCompleted);
    }

    /// <summary>A 1008 close with an unchanged credential faults the client.</summary>
    /// <returns>A task that completes after the fault is checked.</returns>
    [Fact]
    public async Task PolicyViolationClose_WithUnchangedCredential_FaultsCompletion()
    {
        var server = new FakeWorkerChannelServer();
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"));
        var connection = await server.WaitForConnectionAsync(0);

        await connection.CloseAsync(WebSocketCloseStatus.PolicyViolation);

        await Assert.ThrowsAsync<WorkerChannelAuthenticationException>(() => channel.Completion.WaitAsync(Limit));
        Assert.Equal(1, server.ConnectAttempts);
    }

    /// <summary>After a rotation the client closes normally and the next connect carries the new credential.</summary>
    /// <returns>A task that completes after the rotation is checked.</returns>
    [Fact]
    public async Task ReconnectAsync_AfterRotationClosesNormallyAndUsesNewCredential()
    {
        var server = new FakeWorkerChannelServer();
        var credential = new CredentialSource("cred-1");
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            credential);
        var first = await server.WaitForConnectionAsync(0);
        await channel.WaitUntilConnectedAsync(CancellationToken.None).WaitAsync(Limit);

        credential.Value = "cred-2";
        await channel.ReconnectAsync(CancellationToken.None).WaitAsync(Limit);

        Assert.Equal(WebSocketCloseStatus.NormalClosure, await first.Closed.WaitAsync(Limit));
        var second = await server.WaitForConnectionAsync(1);
        Assert.Equal("cred-2", second.Credential);
        Assert.False(channel.Completion.IsCompleted);
    }

    /// <summary>A request while disconnected waits for the request timeout and then fails as unavailable.</summary>
    /// <returns>A task that completes after the timeout is checked.</returns>
    [Fact]
    public async Task Request_WhileDisconnected_TimesOutAsUnavailable()
    {
        var server = new FakeWorkerChannelServer();
        server.FailNextConnects(int.MaxValue);
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"),
            FastOptions() with { RequestTimeout = TimeSpan.FromMilliseconds(300) });
        var timer = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<WorkerChannelUnavailableException>(
            () => channel.HeartbeatAsync(new WorkerHeartbeatRequest(), CancellationToken.None));

        Assert.True(timer.Elapsed >= TimeSpan.FromMilliseconds(250));
        Assert.Null(exception.Outcome);
    }

    /// <summary>An inbound message above the cap closes the socket and the client reconnects.</summary>
    /// <returns>A task that completes after the reconnect is checked.</returns>
    [Fact]
    public async Task OversizedInboundMessage_ClosesSocketAndReconnects()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        await using var channel = await StartAsync(
            server,
            connection,
            new CredentialSource("cred-1"),
            FastOptions() with { MaxInboundMessageBytes = 65536 });
        var first = await server.WaitForConnectionAsync(0);
        var heartbeat = channel.HeartbeatAsync(
            new WorkerHeartbeatRequest { DeploymentId = connection.DeploymentId },
            CancellationToken.None);
        await first.ReadFrameAsync();

        await first.SendOversizedAsync(100 * 1024);

        await Assert.ThrowsAsync<WorkerChannelUnavailableException>(() => heartbeat);
        await server.WaitForConnectionAsync(1);
    }

    /// <summary>A send that stalls past the send timeout aborts the connection and fails the request.</summary>
    /// <returns>A task that completes after the abort and recovery are checked.</returns>
    [Fact]
    public async Task StalledSend_AbortsConnectionAndFailsRequest()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        await using var channel = await StartAsync(
            server,
            connection,
            new CredentialSource("cred-1"),
            FastOptions() with { SendTimeout = TimeSpan.FromMilliseconds(300) });
        var first = await server.WaitForConnectionAsync(0);
        await channel.WaitUntilConnectedAsync(CancellationToken.None).WaitAsync(Limit);
        first.BlockClientWrites();

        await Assert.ThrowsAsync<WorkerChannelUnavailableException>(
            () => channel.HeartbeatAsync(
                new WorkerHeartbeatRequest { DeploymentId = connection.DeploymentId },
                CancellationToken.None));

        var second = await server.WaitForConnectionAsync(1);
        var recovered = channel.HeartbeatAsync(
            new WorkerHeartbeatRequest { DeploymentId = connection.DeploymentId },
            CancellationToken.None);
        await second.AckAsync(await second.ReadFrameAsync());
        await recovered.WaitAsync(Limit);
    }

    /// <summary>A metrics sample on a dropped connection fails and nothing is sent after the reconnect.</summary>
    /// <returns>A task that completes after the drop is checked.</returns>
    [Fact]
    public async Task Metrics_OnDroppedConnectionThrowsAndIsNeverRetried()
    {
        var server = new FakeWorkerChannelServer();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        await using var channel = await StartAsync(server, connection, new CredentialSource("cred-1"));
        var first = await server.WaitForConnectionAsync(0);
        await channel.WaitUntilConnectedAsync(CancellationToken.None).WaitAsync(Limit);

        var send = channel.SendMetricsAsync(CreateSample(connection.DeploymentId), CancellationToken.None);
        var frame = await first.ReadFrameAsync();
        await first.CloseAsync(WebSocketCloseStatus.NormalClosure);

        Assert.Equal(EWorkerChannelFrameType.Metrics, frame.Type);
        await Assert.ThrowsAsync<WorkerChannelUnavailableException>(() => send);
        var second = await server.WaitForConnectionAsync(1);
        Assert.True(await second.StaysSilentAsync(TimeSpan.FromMilliseconds(300)));
    }

    /// <summary>A metrics sample while disconnected fails at once instead of waiting for a connection.</summary>
    /// <returns>A task that completes after the failure is checked.</returns>
    [Fact]
    public async Task Metrics_WhileDisconnected_ThrowsWithoutWaiting()
    {
        var server = new FakeWorkerChannelServer();
        server.FailNextConnects(int.MaxValue);
        await using var channel = await StartAsync(
            server,
            CloudWorkerBootstrapConfigTests.CreateBootstrap(),
            new CredentialSource("cred-1"));
        var timer = Stopwatch.StartNew();

        await Assert.ThrowsAsync<WorkerChannelUnavailableException>(
            () => channel.SendMetricsAsync(CreateSample(Guid.NewGuid()), CancellationToken.None));

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2));
    }

    /// <summary>Console diagnostics never contain the credential or a frame body.</summary>
    /// <returns>A task that completes after the captured output is checked.</returns>
    [Fact]
    public async Task ConsoleOutput_NeverContainsCredentialOrFramePayload()
    {
        const string secretCredential = "credential-value-must-not-leak";
        const string secretPayload = "frame-payload-must-not-leak";
        var original = Console.Error;
        var captured = new StringWriter();
        Console.SetError(TextWriter.Synchronized(captured));
        try
        {
            var server = new FakeWorkerChannelServer();
            server.FailNextConnects(1);
            var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap();
            await using (var channel = await StartAsync(server, connection, new CredentialSource(secretCredential)))
            {
                var first = await server.WaitForConnectionAsync(0);
                var report = channel.ReportCommandAsync(
                    Guid.NewGuid(),
                    new WorkerCommandReportRequest(
                        connection.DeploymentId,
                        EWorkerCommandReportState.Running,
                        "running",
                        DateTimeOffset.UtcNow,
                        null,
                        null,
                        secretPayload,
                        1,
                        null),
                    CancellationToken.None);
                await first.ReadFrameAsync();
                await first.CloseAsync(WebSocketCloseStatus.EndpointUnavailable);
                await Assert.ThrowsAsync<WorkerChannelUnavailableException>(() => report);
                await server.WaitForConnectionAsync(1);
            }
        }
        finally
        {
            Console.SetError(original);
        }

        var output = captured.ToString();
        Assert.Contains("Cloud worker channel", output);
        Assert.DoesNotContain(secretCredential, output);
        Assert.DoesNotContain(secretPayload, output);
    }

    private static CloudWorkerChannelOptions FastOptions()
        => new(
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(100),
            4194304);

    private static CloudWorkerChannelOptions ManualTimeOptions()
        => new(
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(98),
            TimeSpan.FromSeconds(99),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            4194304);

    private static WorkerMetricsSample CreateSample(Guid deploymentId)
        => new()
        {
            DeploymentId     = deploymentId,
            SampledAt        = DateTimeOffset.UtcNow,
            CpuPercent       = 12.5,
            MemoryUsedBytes  = 1024,
            MemoryTotalBytes = 4096,
        };

    private static async Task<CloudWorkerChannelClient> StartAsync(
        FakeWorkerChannelServer server,
        CloudWorkerConnectionConfig connection,
        CredentialSource credential,
        CloudWorkerChannelOptions? options = null,
        TimeProvider? time = null)
    {
        var channel = new CloudWorkerChannelClient(server, options ?? FastOptions(), time);
        await channel.StartAsync(connection, credential.Provider, CancellationToken.None);
        return channel;
    }

    /// <summary>A mutable credential the client re-reads before every connect attempt.</summary>
    private sealed class CredentialSource(string initial)
    {
        /// <summary>Gets or sets the credential the provider returns.</summary>
        /// <value>The current credential.</value>
        public string Value { get; set; } = initial;

        /// <summary>Gets the provider delegate handed to the client.</summary>
        /// <value>A delegate that returns the current credential.</value>
        public Func<CancellationToken, Task<string>> Provider => _ => Task.FromResult(Value);
    }
}
