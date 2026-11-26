using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves the worker channel over a real loopback WebSocket pair: the wake after connect, correlated acks for
/// heartbeats, claims and reports, and the failure outcomes that mirror the HTTP protocol.
/// </summary>
public sealed class WorkerChannelHandlerTests
{
    [Fact]
    public async Task Connect_SendsAWakeFrameFirst()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var first = await client.ReceiveAsync();

        Assert.Equal(EWorkerChannelFrameType.Wake, first.Type);
        Assert.Null(first.Id);
    }

    [Fact]
    public async Task Heartbeat_IsAckedAcceptedWithTheRequestIdAndPersisted()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        var requestId = Guid.NewGuid();

        var ack = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.Heartbeat,
            requestId,
            new WorkerHeartbeatRequest { DeploymentId = worker.DeploymentId }));

        Assert.Equal(EWorkerChannelFrameType.Ack, ack.Type);
        Assert.Equal(requestId, ack.Id);
        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        var persisted = await host.Store.DbContext.Set<LifecycleWorker>()
            .AsNoTracking()
            .SingleAsync(q => q.PrimaryKey == worker.WorkerId);
        Assert.NotNull(persisted.LastHeartbeatAt);
    }

    [Fact]
    public async Task Claim_AckCarriesTheClaimResponseAndNothingToDeliverIsANullPayload()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, commandId) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var claimed = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.Claim,
            Guid.NewGuid(),
            new WorkerCommandClaimRequest { DeploymentId = worker.DeploymentId }));

        Assert.Equal(EWorkerChannelOutcome.Accepted, claimed.Outcome);
        var response = claimed.Payload!.Value.Deserialize<WorkerCommandClaimResponse>(WorkerChannelJson.Options);
        Assert.Equal(commandId, response!.CommandId);

        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        command.Status = EWorkerCommandStatus.Succeeded;
        await host.Store.DbContext.SaveChangesAsync();
        var empty = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.Claim,
            Guid.NewGuid(),
            new WorkerCommandClaimRequest { DeploymentId = worker.DeploymentId }));

        Assert.Equal(EWorkerChannelOutcome.Accepted, empty.Outcome);
        Assert.Null(empty.Payload);
    }

    [Fact]
    public async Task Reports_AreAckedAcceptedAndTheCommandEndsSucceeded()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, commandId) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        var processStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var received = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.ReportReceived,
            Guid.NewGuid(),
            new WorkerCommandStageRequest { DeploymentId = worker.DeploymentId, Stage = "received" },
            commandId));
        var running = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.ReportRunning,
            Guid.NewGuid(),
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "running",
                ProcessId = 11,
                ProcessStartedAt = processStartedAt,
            },
            commandId));
        var succeeded = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.ReportSucceeded,
            Guid.NewGuid(),
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "done",
                ProcessId = 11,
                ProcessStartedAt = processStartedAt,
            },
            commandId));

        Assert.All(
            new[] { received, running, succeeded },
            ack => Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome));
        var command = await host.Store.DbContext.Set<WorkerCommand>()
            .AsNoTracking()
            .SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Succeeded, command.Status);
    }

    [Fact]
    public async Task MessageAboveTheCap_ClosesTheChannelWithMessageTooBig()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();

        await client.SendTextAsync(new string('x', 300 * 1024));
        var status = await client.ReceiveCloseAsync();

        Assert.Equal(WebSocketCloseStatus.MessageTooBig, status);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task UnreadableFrameWithAReadableId_IsAckedRejectedAndTheConnectionStaysOpen()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        var requestId = Guid.NewGuid();

        await client.SendTextAsync("{\"id\":\"" + requestId + "\",\"type\":\"Heartbeat\",\"commandId\":\"not-a-guid\"}");
        var ack = await ReceiveAckAsync(client, requestId);

        Assert.Equal(EWorkerChannelOutcome.Rejected, ack.Outcome);
        await AssertStillServingAsync(client, worker);
    }

    [Fact]
    public async Task UnknownFrameTypeString_IsAckedRejectedAndTheConnectionStaysOpen()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        var requestId = Guid.NewGuid();

        await client.SendTextAsync("{\"id\":\"" + requestId + "\",\"type\":\"Teleport\"}");
        var ack = await ReceiveAckAsync(client, requestId);

        Assert.Equal(EWorkerChannelOutcome.Rejected, ack.Outcome);
        Assert.Equal("The frame type is unsupported or the frame is invalid.", ack.Error);
        await AssertStillServingAsync(client, worker);
    }

    [Theory]
    [InlineData(EWorkerChannelFrameType.Metrics)]
    [InlineData(EWorkerChannelFrameType.Ack)]
    [InlineData(EWorkerChannelFrameType.Wake)]
    public async Task FrameTypeWithoutARoute_IsAckedRejectedAndTheConnectionStaysOpen(EWorkerChannelFrameType type)
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(new WorkerChannelFrame { Type = type, Id = Guid.NewGuid() });

        Assert.Equal(EWorkerChannelOutcome.Rejected, ack.Outcome);
        Assert.Equal("The frame type is unsupported or the frame is invalid.", ack.Error);
        await AssertStillServingAsync(client, worker);
    }

    [Fact]
    public async Task MessageWithoutAReadableId_IsIgnoredAndTheConnectionStaysOpen()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        await client.SendTextAsync("this is not json");
        await client.SendTextAsync("{\"type\":\"Heartbeat\"}");

        await AssertStillServingAsync(client, worker);
    }

    [Fact]
    public async Task HeartbeatWithInvalidStorageTelemetry_IsAckedRejectedWithASanitizedError()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.Heartbeat,
            Guid.NewGuid(),
            new WorkerHeartbeatRequest
            {
                DeploymentId = worker.DeploymentId,
                DeploymentStorageTotalBytes = 100,
                DeploymentStorageAvailableBytes = 101,
                ControlledDeploymentBytes = 0,
            }));

        Assert.Equal(EWorkerChannelOutcome.Rejected, ack.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(ack.Error));
        Assert.True(ack.Error.Length <= 512);
    }

    [Fact]
    public async Task ReportForAnUnknownCommand_IsAckedRejected()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.ReportReceived,
            Guid.NewGuid(),
            new WorkerCommandStageRequest { DeploymentId = worker.DeploymentId, Stage = "received" },
            Guid.NewGuid()));

        Assert.Equal(EWorkerChannelOutcome.Rejected, ack.Outcome);
    }

    [Fact]
    public async Task ReportRunningAfterCostStopSupersededTheCommand_IsAckedSuperseded()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, commandId) = await host.SeedRegisteredWorkerAsync();
        var deployment = await host.Store.DbContext.Set<GameServerDeployment>()
            .SingleAsync(q => q.PrimaryKey == worker.DeploymentId);
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.Terminating;
        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        command.Status = EWorkerCommandStatus.CancelledByCostStop;
        await host.Store.DbContext.SaveChangesAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.ReportRunning,
            Guid.NewGuid(),
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "late",
                ProcessId = 1,
                ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            commandId));

        Assert.Equal(EWorkerChannelOutcome.Superseded, ack.Outcome);
        Assert.Equal(EWorkerCommandStatus.CancelledByCostStop, command.Status);
    }

    [Fact]
    public async Task UnexpectedRouteFailure_IsAckedUnavailableWithoutLeakingTheExceptionText()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await WorkerChannelTestHost.CreateAsync(
            loggerProvider: logs,
            configure: services => services.AddScoped<IWorkerChannelFrameRoute, ThrowingMetricsRoute>());
        await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();

        var ack = await client.RequestAsync(new WorkerChannelFrame
        {
            Type = EWorkerChannelFrameType.Metrics,
            Id = Guid.NewGuid(),
        });

        Assert.Equal(EWorkerChannelOutcome.Unavailable, ack.Outcome);
        Assert.Equal("The control plane could not process the request.", ack.Error);
        Assert.DoesNotContain("boom-secret-detail", ack.Error, StringComparison.Ordinal);
        Assert.Contains(
            logs.Messages,
            q => q.Contains("Worker channel frame failed", StringComparison.Ordinal)
                 && q.Contains(nameof(EWorkerChannelFrameType.Metrics), StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevokedCredential_ClosesTheChannelWithPolicyViolationAndNeverAcks()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();
        var row = await host.Store.DbContext.Set<LifecycleWorker>().SingleAsync(q => q.PrimaryKey == worker.WorkerId);
        row.WorkerCredentialRevokedAt = NodaSystemClock.Instance.GetCurrentInstant();
        await host.Store.DbContext.SaveChangesAsync();

        await client.SendAsync(Frame(
            EWorkerChannelFrameType.Heartbeat,
            Guid.NewGuid(),
            new WorkerHeartbeatRequest { DeploymentId = worker.DeploymentId }));
        var status = await client.ReceiveCloseAsync();

        Assert.Equal(WebSocketCloseStatus.PolicyViolation, status);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Push_ThroughTheRegistryReachesTheConnectedWorker()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();
        var pushId = Guid.NewGuid();

        var delivered = await host.Registry.TrySendAsync(
            worker.WorkerId,
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake, Id = pushId },
            CancellationToken.None);
        var received = await client.ReceiveAsync();

        Assert.True(delivered);
        Assert.Equal(EWorkerChannelFrameType.Wake, received.Type);
        Assert.Equal(pushId, received.Id);
    }

    [Fact]
    public async Task Push_ForAWorkerWithoutAConnectionReturnsFalse()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        await host.SeedRegisteredWorkerAsync();

        var delivered = await host.Registry.TrySendAsync(
            Guid.NewGuid(),
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake },
            CancellationToken.None);

        Assert.False(delivered);
        Assert.Equal(0, host.Registry.ConnectionCount);
    }

    [Fact]
    public async Task PushToDeployment_ReachesEveryConnectionOfThatDeploymentAndNoOther()
    {
        var registry = new WorkerChannelRegistry(NullLogger<WorkerChannelRegistry>.Instance);
        var deploymentA = Guid.NewGuid();
        var deploymentB = Guid.NewGuid();
        var firstOfA = new FakeConnection(Guid.NewGuid(), deploymentA);
        var secondOfA = new FakeConnection(Guid.NewGuid(), deploymentA);
        var ofB = new FakeConnection(Guid.NewGuid(), deploymentB);
        using var registrations = new CompositeDisposable(
            registry.Register(firstOfA),
            registry.Register(secondOfA),
            registry.Register(ofB));
        var frame = new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake, Id = Guid.NewGuid() };

        await registry.SendToDeploymentAsync(deploymentA, frame, CancellationToken.None);

        Assert.Equal([frame], firstOfA.Sent);
        Assert.Equal([frame], secondOfA.Sent);
        Assert.Empty(ofB.Sent);
        Assert.Equal(3, registry.ConnectionCount);
    }

    [Fact]
    public async Task ConcurrentPushesAndAcks_ArriveAsCompleteFrames()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync(configureOptions: options =>
        {
            options.ChannelFrameBurst = 1000;
            options.ChannelMaxFramesPerSecond = 1000;
        });
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();
        var pushIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();
        var requestIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();

        var pushes = pushIds
            .Select(id => Task.Run(() => host.Registry.TrySendAsync(
                worker.WorkerId,
                new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake, Id = id },
                CancellationToken.None)))
            .ToArray();
        var requests = Task.Run(async () =>
        {
            foreach (var id in requestIds)
            {
                await client.SendAsync(Frame(
                    EWorkerChannelFrameType.Heartbeat,
                    id,
                    new WorkerHeartbeatRequest { DeploymentId = worker.DeploymentId }));
            }
        });
        var ackIds = new HashSet<Guid>();
        var wakeIds = new HashSet<Guid>();
        while (ackIds.Count < requestIds.Length || wakeIds.Count < pushIds.Length)
        {
            var frame = await client.ReceiveAsync();
            if (frame is { Type: EWorkerChannelFrameType.Ack, Id: { } ackId })
                ackIds.Add(ackId);
            else if (frame is { Type: EWorkerChannelFrameType.Wake, Id: { } wakeId })
                wakeIds.Add(wakeId);
        }
        await requests;

        Assert.All(await Task.WhenAll(pushes), Assert.True);
        Assert.Equal(requestIds.ToHashSet(), ackIds);
        Assert.Equal(pushIds.ToHashSet(), wakeIds);
    }

    [Fact]
    public async Task NewerConnection_ReplacesTheOlderOneAndDisposingTheOlderRegistrationKeepsTheNewerOne()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var older = await host.ConnectAsync();
        await older.ReceiveAsync();
        await using var newer = await host.ConnectAsync();

        var newerFirst = await newer.ReceiveAsync();
        var closeStatus = await older.ReceiveCloseAsync();

        Assert.Equal(EWorkerChannelFrameType.Wake, newerFirst.Type);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
        Assert.Equal("replaced by a newer connection", older.Socket.CloseStatusDescription);
        Assert.Equal(1, host.Registry.ConnectionCount);

        await older.DisposeAsync();

        Assert.Equal(1, host.Registry.ConnectionCount);
        var pushId = Guid.NewGuid();
        Assert.True(await host.Registry.TrySendAsync(
            worker.WorkerId,
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake, Id = pushId },
            CancellationToken.None));
        Assert.Equal(pushId, (await newer.ReceiveAsync()).Id);
    }

    [Fact]
    public async Task ClosingTheSocket_RemovesTheRegistryEntry()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        var before = host.Registry.ConnectionCount;
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();
        Assert.Equal(before + 1, host.Registry.ConnectionCount);

        await client.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(before, host.Registry.ConnectionCount);
        Assert.False(await host.Registry.TrySendAsync(
            worker.WorkerId,
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake },
            CancellationToken.None));
    }

    [Fact]
    public async Task Registry_DisposingAReplacedRegistrationNeverRemovesTheNewerConnection()
    {
        var registry = new WorkerChannelRegistry(NullLogger<WorkerChannelRegistry>.Instance);
        var workerId = Guid.NewGuid();
        var older = new FakeConnection(workerId, Guid.NewGuid());
        var newer = new FakeConnection(workerId, Guid.NewGuid());
        var olderRegistration = registry.Register(older);
        using var newerRegistration = registry.Register(newer);

        olderRegistration.Dispose();
        olderRegistration.Dispose();

        Assert.Equal(1, registry.ConnectionCount);
        Assert.Equal((WebSocketCloseStatus.NormalClosure, "replaced by a newer connection"), Assert.Single(older.Closed));
        Assert.Empty(newer.Closed);
        Assert.True(await registry.TrySendAsync(
            workerId,
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake },
            CancellationToken.None));
        Assert.Single(newer.Sent);
    }

    [Fact]
    public async Task Registry_FailedPushIsLoggedWithoutBodyReturnsFalseAndDropsTheConnection()
    {
        var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var registry = new WorkerChannelRegistry(loggerFactory.CreateLogger<WorkerChannelRegistry>());
        var connection = new FakeConnection(Guid.NewGuid(), Guid.NewGuid())
        {
            SendFailure = new WebSocketException("socket-detail-that-must-not-leak"),
        };
        using var registration = registry.Register(connection);

        var delivered = await registry.TrySendAsync(
            connection.WorkerId,
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake, Error = "frame-body-that-must-not-leak" },
            CancellationToken.None);

        Assert.False(delivered);
        Assert.Equal(0, registry.ConnectionCount);
        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(
            logs.Messages,
            q => q.Contains("must-not-leak", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FrameBurstAboveTheLimit_ClosesWithPolicyViolationBeforeEveryFrameIsAcked()
    {
        var clock = new ManualTimeProvider();
        await using var host = await WorkerChannelTestHost.CreateAsync(
            timeProvider: clock,
            configureOptions: options =>
            {
                options.ChannelFrameBurst = 40;
                options.ChannelMaxFramesPerSecond = 20;
            });
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();

        for (var i = 0; i < 60; i++)
            await client.SendAsync(Heartbeat(worker));
        var acked = await CountAcksUntilCloseAsync(client);

        Assert.Equal(40, acked);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, client.Socket.CloseStatus);
        Assert.Equal("frame rate exceeded", client.Socket.CloseStatusDescription);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, host.Registry.ConnectionCount);
    }

    [Fact]
    public async Task FrameRate_RefillsWithElapsedTime()
    {
        var clock = new ManualTimeProvider();
        await using var host = await WorkerChannelTestHost.CreateAsync(
            timeProvider: clock,
            configureOptions: options =>
            {
                options.ChannelFrameBurst = 5;
                options.ChannelMaxFramesPerSecond = 2;
            });
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();

        for (var i = 0; i < 5; i++)
            await AssertStillServingAsync(client, worker, drainHeartbeatWake: true);
        clock.Advance(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 2; i++)
            await AssertStillServingAsync(client, worker, drainHeartbeatWake: true);
        await client.SendAsync(Heartbeat(worker));
        var status = await client.ReceiveCloseAsync();

        Assert.Equal(WebSocketCloseStatus.PolicyViolation, status);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task SendThatDoesNotFinishWithinTheTimeout_AbortsTheConnectionAndRemovesTheRegistryEntry()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync(
            configureOptions: options => options.ChannelSendTimeout = TimeSpan.FromMilliseconds(300));
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();
        var bulk = new WorkerChannelFrame
        {
            Type = EWorkerChannelFrameType.Wake,
            Payload = JsonSerializer.SerializeToElement(new string('x', 2 * 1024 * 1024)),
        };

        // The client never reads, so the socket buffers fill and one push stalls until the send timeout fires.
        var sawFailedPush = false;
        for (var i = 0; i < 400 && !sawFailedPush; i++)
            sawFailedPush = !await host.Registry.TrySendAsync(worker.WorkerId, bulk, CancellationToken.None);

        Assert.True(sawFailedPush);
        Assert.Equal(0, host.Registry.ConnectionCount);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CredentialRotation_KeepsTheOldSocketUntilTheReplacementIsPromotedThenRefusesTheOldCredential()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();

        var replacement = await RotateAsync(host, worker, credential);

        await AssertStillServingAsync(client, worker, drainHeartbeatWake: true);
        await AuthenticateAsync(host, worker, replacement);
        await client.SendAsync(Heartbeat(worker));
        var status = await client.ReceiveCloseAsync();

        Assert.Equal(WebSocketCloseStatus.PolicyViolation, status);
        await client.ServerTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, host.Registry.ConnectionCount);
    }

    [Fact]
    public async Task CredentialRotation_NewSocketPromotesTheReplacementAndReplacesTheOldConnection()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        await using var older = await host.ConnectAsync();
        await older.ReceiveAsync();
        var replacement = await RotateAsync(host, worker, credential);

        await using var newer = await host.ConnectAsync(replacement);
        var closeStatus = await older.ReceiveCloseAsync();

        Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
        Assert.Equal("replaced by a newer connection", older.Socket.CloseStatusDescription);
        await AssertStillServingAsync(newer, worker, skipWake: true);
        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => host.ConnectAsync(credential));
        Assert.Equal(1, host.Registry.ConnectionCount);
    }

    [Fact]
    public async Task CommandClaimedBeforeARestart_IsRedeliveredWithTheSameAttemptNumberAfterIt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        WorkerCommandClaimResponse first;
        (AuthenticatedWorker Worker, string Credential, Guid CommandId) seeded;
        await using (var before = await WorkerChannelTestHost.CreateAsync(store))
        {
            seeded = await before.SeedRegisteredWorkerAsync();
            await using var client = await before.ConnectAsync();
            first = await ClaimAsync(client, seeded.Worker);
            await client.RequestAsync(Frame(
                EWorkerChannelFrameType.ReportReceived,
                Guid.NewGuid(),
                new WorkerCommandStageRequest { DeploymentId = seeded.Worker.DeploymentId, Stage = "received" },
                first.CommandId));
            await client.RequestAsync(Frame(
                EWorkerChannelFrameType.ReportRunning,
                Guid.NewGuid(),
                new WorkerCommandProcessRequest
                {
                    DeploymentId = seeded.Worker.DeploymentId,
                    Stage = "running",
                    ProcessId = 21,
                    ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                },
                first.CommandId));
        }

        await using var after = await WorkerChannelTestHost.CreateAsync(store);
        after.AdoptSeededWorker(seeded);
        await using var restarted = await after.ConnectAsync();
        var second = await ClaimAsync(restarted, seeded.Worker);

        Assert.Equal(seeded.CommandId, first.CommandId);
        Assert.Equal(first.CommandId, second.CommandId);
        Assert.Equal(first.Attempt, second.Attempt);
    }

    [Fact]
    public async Task ChannelLogs_NeverContainACommandPayloadOrACredential()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await WorkerChannelTestHost.CreateAsync(
            loggerProvider: logs,
            configure: services => services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace)));
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        await client.ReceiveAsync();

        var claimed = await ClaimAsync(client, worker);
        await client.RequestAsync(Frame(
            EWorkerChannelFrameType.ReportReceived,
            Guid.NewGuid(),
            new WorkerCommandStageRequest { DeploymentId = worker.DeploymentId, Stage = "received" },
            claimed.CommandId));
        await client.SendTextAsync("{\"id\":\"" + Guid.NewGuid() + "\",\"type\":\"Teleport\",\"error\":\"" + credential + "\"}");
        await client.RequestAsync(new WorkerChannelFrame { Type = EWorkerChannelFrameType.Ack, Id = Guid.NewGuid() });
        var replacement = await RotateAsync(host, worker, credential);
        await using var newer = await host.ConnectAsync(replacement);
        await newer.ReceiveAsync();
        await host.Registry.TrySendAsync(
            worker.WorkerId,
            new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake, Id = Guid.NewGuid() },
            CancellationToken.None);
        await newer.ReceiveAsync();
        await newer.SendAsync(Heartbeat(worker));
        await newer.ReceiveAsync();

        Assert.Equal("{\"test\":true}", claimed.Payload);
        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(
            logs.Messages,
            q => q.Contains("\"test\":true", StringComparison.Ordinal)
                 || q.Contains(credential, StringComparison.Ordinal)
                 || q.Contains(replacement, StringComparison.Ordinal));
    }

    private static async Task<WorkerChannelFrame> ReceiveAckAsync(
        WorkerChannelTestHost.WorkerChannelTestClient client,
        Guid requestId)
    {
        while (true)
        {
            var frame = await client.ReceiveAsync();
            if (frame is { Type: EWorkerChannelFrameType.Ack } && frame.Id == requestId)
                return frame;
        }
    }

    private static async Task AssertStillServingAsync(
        WorkerChannelTestHost.WorkerChannelTestClient client,
        AuthenticatedWorker worker,
        bool skipWake = false,
        bool drainHeartbeatWake = false)
    {
        if (skipWake)
            await client.ReceiveAsync();
        var ack = await client.RequestAsync(Heartbeat(worker));
        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        // A heartbeat of a worker with a deliverable command is followed by a safety-net Wake; consume it so a
        // later close frame is the next message.
        if (drainHeartbeatWake)
            Assert.Equal(EWorkerChannelFrameType.Wake, (await client.ReceiveAsync()).Type);
    }

    private static WorkerChannelFrame Heartbeat(AuthenticatedWorker worker)
        => Frame(
            EWorkerChannelFrameType.Heartbeat,
            Guid.NewGuid(),
            new WorkerHeartbeatRequest { DeploymentId = worker.DeploymentId });

    private static async Task<WorkerCommandClaimResponse> ClaimAsync(
        WorkerChannelTestHost.WorkerChannelTestClient client,
        AuthenticatedWorker worker)
    {
        var ack = await client.RequestAsync(Frame(
            EWorkerChannelFrameType.Claim,
            Guid.NewGuid(),
            new WorkerCommandClaimRequest { DeploymentId = worker.DeploymentId }));
        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        return ack.Payload!.Value.Deserialize<WorkerCommandClaimResponse>(WorkerChannelJson.Options)!;
    }

    private static async Task<int> CountAcksUntilCloseAsync(WorkerChannelTestHost.WorkerChannelTestClient client)
    {
        var acked = 0;
        while (true)
        {
            try
            {
                if ((await client.ReceiveAsync()).Type is EWorkerChannelFrameType.Ack)
                    acked++;
            }
            catch (InvalidOperationException)
            {
                return acked;
            }
        }
    }

    private static async Task<string> RotateAsync(WorkerChannelTestHost host, AuthenticatedWorker worker, string credential)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var rotation = await scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>().RotateCredentialAsync(
            worker.WorkerId,
            new WorkerCredentialRotationRequest { DeploymentId = worker.DeploymentId },
            credential);
        return rotation.WorkerCredential;
    }

    private static async Task AuthenticateAsync(WorkerChannelTestHost host, AuthenticatedWorker worker, string credential)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>()
            .AuthenticateAsync(worker.WorkerId, worker.DeploymentId, credential);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
    }

    private sealed class ThrowingMetricsRoute : IWorkerChannelFrameRoute
    {
        public IReadOnlyCollection<EWorkerChannelFrameType> FrameTypes { get; } = [EWorkerChannelFrameType.Metrics];

        public Task<WorkerChannelRouteResult> HandleAsync(
            WorkerChannelRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom-secret-detail");
    }

    private sealed class FakeConnection(Guid workerId, Guid deploymentId) : IWorkerChannelConnection
    {
        public Guid WorkerId { get; } = workerId;

        public Guid DeploymentId { get; } = deploymentId;

        public Exception? SendFailure { get; init; }

        public List<WorkerChannelFrame> Sent { get; } = [];

        public List<(WebSocketCloseStatus Status, string Description)> Closed { get; } = [];

        public Task SendAsync(WorkerChannelFrame frame, CancellationToken cancellationToken)
        {
            if (SendFailure is not null)
                return Task.FromException(SendFailure);
            lock (Sent)
                Sent.Add(frame);
            return Task.CompletedTask;
        }

        public Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken)
        {
            Closed.Add((status, description));
            return Task.CompletedTask;
        }
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in disposables)
                disposable.Dispose();
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return _messages.ToArray();
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (owner._messages)
                    owner._messages.Add(formatter(state, exception));
            }
        }
    }

    private static WorkerChannelFrame Frame<TPayload>(
        EWorkerChannelFrameType type,
        Guid id,
        TPayload payload,
        Guid? commandId = null)
        => new()
        {
            Type = type,
            Id = id,
            CommandId = commandId,
            Payload = JsonSerializer.SerializeToElement(payload, WorkerChannelJson.Options),
        };
}
