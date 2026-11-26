using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that worker channel frames are executed by the unchanged protocol service with the same results and
/// failures as the HTTP worker endpoints.
/// </summary>
public sealed class WorkerChannelRouteTests
{
    [Fact]
    public async Task Heartbeat_PersistsProcessEvidenceAndReturnsEmptyResult()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        var staleHeartbeat = Instant.FromUnixTimeSeconds(1);
        var row = await host.Store.DbContext.Set<LifecycleWorker>().SingleAsync(q => q.PrimaryKey == worker.WorkerId);
        row.LastHeartbeatAt = staleHeartbeat;
        await host.Store.DbContext.SaveChangesAsync();
        var startedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var result = await host.RouteAsync(HeartbeatRequest(
            worker,
            credential,
            new WorkerHeartbeatRequest
            {
                DeploymentId = worker.DeploymentId,
                ProcessId = 4242,
                ProcessStartedAt = startedAt,
            }));

        // The seeded Queued command is deliverable, so the heartbeat also requests the missed-push safety-net Wake.
        Assert.Null(result.Result);
        Assert.True(result.WakeAfterAck);
        var persisted = await ReadWorkerAsync(host, worker);
        Assert.True(persisted.LastHeartbeatAt > staleHeartbeat);
        Assert.Equal(4242, persisted.ProcessId);
        Assert.Equal(Instant.FromDateTimeOffset(startedAt), persisted.ProcessStartedAt);
    }

    [Fact]
    public void Frame_SerializesTypeAndOutcomeAsNamesAndRoundTrips()
    {
        var frame = new WorkerChannelFrame
        {
            Type = EWorkerChannelFrameType.Ack,
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            Outcome = EWorkerChannelOutcome.Accepted,
            Error = "bounded",
            Payload = JsonSerializer.SerializeToElement(
                new WorkerHeartbeatRequest { DeploymentId = Guid.NewGuid(), ProcessId = 7 },
                WorkerChannelJson.Options),
        };

        var json = JsonSerializer.Serialize(frame, WorkerChannelJson.Options);
        using var document = JsonDocument.Parse(json);
        var roundTripped = JsonSerializer.Deserialize<WorkerChannelFrame>(json, WorkerChannelJson.Options);

        Assert.Equal("Ack", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Accepted", document.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("payload").ValueKind);
        Assert.NotNull(roundTripped);
        Assert.Equal(frame.Type, roundTripped.Type);
        Assert.Equal(frame.Id, roundTripped.Id);
        Assert.Equal(frame.CommandId, roundTripped.CommandId);
        Assert.Equal(frame.Outcome, roundTripped.Outcome);
        Assert.Equal(frame.Error, roundTripped.Error);
        Assert.Equal(frame.Payload!.Value.GetRawText(), roundTripped.Payload!.Value.GetRawText());
    }

    [Fact]
    public async Task Heartbeat_WithoutPayload_IsRejectedAndChangesNothing()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        var before = await ReadWorkerAsync(host, worker);

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => host.RouteAsync(
            new WorkerChannelRequest(
                worker,
                credential,
                new WorkerChannelFrame { Type = EWorkerChannelFrameType.Heartbeat, Id = Guid.NewGuid() })));

        await AssertWorkerUnchangedAsync(host, worker, before);
    }

    [Fact]
    public async Task Heartbeat_WithArrayPayload_IsRejectedAndChangesNothing()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        var before = await ReadWorkerAsync(host, worker);

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => host.RouteAsync(
            new WorkerChannelRequest(
                worker,
                credential,
                new WorkerChannelFrame
                {
                    Type = EWorkerChannelFrameType.Heartbeat,
                    Id = Guid.NewGuid(),
                    Payload = JsonDocument.Parse("[1,2,3]").RootElement.Clone(),
                })));

        await AssertWorkerUnchangedAsync(host, worker, before);
    }

    [Fact]
    public async Task Heartbeat_WithInvalidStorageTelemetry_IsRejectedAndChangesNothing()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        var before = await ReadWorkerAsync(host, worker);

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => host.RouteAsync(HeartbeatRequest(
            worker,
            credential,
            new WorkerHeartbeatRequest
            {
                DeploymentId = worker.DeploymentId,
                DeploymentStorageTotalBytes = 10,
                DeploymentStorageAvailableBytes = 50,
                ControlledDeploymentBytes = 1,
            })));

        await AssertWorkerUnchangedAsync(host, worker, before);
    }

    [Fact]
    public async Task Heartbeat_ForAnotherDeployment_IsRejectedAndChangesNothing()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, _) = await host.SeedRegisteredWorkerAsync();
        var before = await ReadWorkerAsync(host, worker);

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => host.RouteAsync(HeartbeatRequest(
            worker,
            credential,
            new WorkerHeartbeatRequest
            {
                DeploymentId = Guid.NewGuid(),
                ProcessId = 1,
                ProcessStartedAt = DateTimeOffset.UtcNow,
            })));

        await AssertWorkerUnchangedAsync(host, worker, before);
    }

    [Fact]
    public async Task Heartbeat_WithWrongCredential_IsRejectedByTheProtocolService()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, _) = await host.SeedRegisteredWorkerAsync();
        var before = await ReadWorkerAsync(host, worker);

        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => host.RouteAsync(HeartbeatRequest(
            worker,
            "not-the-worker-credential",
            new WorkerHeartbeatRequest { DeploymentId = worker.DeploymentId })));

        await AssertWorkerUnchangedAsync(host, worker, before);
    }

    [Fact]
    public async Task Claim_ReturnsTheQueuedCommandAndRedeliversItBeforeAndAfterReceipt()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();

        var first = await ClaimAsync(host, worker, credential);
        var repeated = await ClaimAsync(host, worker, credential);
        await host.RouteAsync(StageReport(worker, credential, commandId, worker.DeploymentId));
        var afterReceipt = await ClaimAsync(host, worker, credential);

        Assert.NotNull(first);
        Assert.Equal(commandId, first.CommandId);
        Assert.Equal(worker.WorkerId, first.WorkerId);
        Assert.Equal(worker.DeploymentId, first.DeploymentId);
        Assert.Equal(1, first.Attempt);
        Assert.Equal("{\"test\":true}", first.Payload);
        Assert.True(first.LeaseExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(commandId, repeated?.CommandId);
        Assert.Equal(1, repeated?.Attempt);
        Assert.Equal(commandId, afterReceipt?.CommandId);
    }

    [Fact]
    public async Task Claim_WithNothingToDeliver_ReturnsNullResult()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();
        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        command.Status = EWorkerCommandStatus.Failed;
        await host.Store.DbContext.SaveChangesAsync();

        var result = await host.RouteAsync(ClaimRequest(worker, credential, worker.DeploymentId));

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task Claim_ForAnotherDeployment_IsRejected()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(
            () => host.RouteAsync(ClaimRequest(worker, credential, Guid.NewGuid())));

        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Queued, command.Status);
    }

    [Fact]
    public async Task Claim_WhileCostStopForbidsExecution_ThrowsTheProtocolConflict()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();
        await EnterCostStopAsync(host, worker);

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(
            () => host.RouteAsync(ClaimRequest(worker, credential, worker.DeploymentId)));

        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Queued, command.Status);
    }

    [Fact]
    public async Task Reports_MoveTheCommandThroughReceivedRunningAndSucceeded()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();
        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var received = await host.RouteAsync(StageReport(worker, credential, commandId, worker.DeploymentId));
        var afterReceived = command.Status;
        var running = await host.RouteAsync(ProcessReport(
            EWorkerChannelFrameType.ReportRunning,
            worker,
            credential,
            commandId,
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "starting",
                ProcessId = 4242,
                ProcessStartedAt = startedAt,
            }));
        var afterRunning = command.Status;
        var succeeded = await host.RouteAsync(ProcessReport(
            EWorkerChannelFrameType.ReportSucceeded,
            worker,
            credential,
            commandId,
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "started",
                ProcessId = 4242,
                ProcessStartedAt = startedAt,
            }));

        Assert.Same(WorkerChannelRouteResult.Empty, received);
        Assert.Same(WorkerChannelRouteResult.Empty, running);
        Assert.Same(WorkerChannelRouteResult.Empty, succeeded);
        Assert.Equal(EWorkerCommandStatus.Received, afterReceived);
        Assert.Equal(EWorkerCommandStatus.Running, afterRunning);
        Assert.Equal(EWorkerCommandStatus.Succeeded, command.Status);
    }

    [Fact]
    public async Task ReportFailed_EndsTheCommandFailedWithTheSentErrorCode()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();

        await host.RouteAsync(new WorkerChannelRequest(
            worker,
            credential,
            new WorkerChannelFrame
            {
                Type = EWorkerChannelFrameType.ReportFailed,
                Id = Guid.NewGuid(),
                CommandId = commandId,
                Payload = JsonSerializer.SerializeToElement(
                    new WorkerCommandFailureRequest
                    {
                        DeploymentId = worker.DeploymentId,
                        Stage = "install",
                        ErrorCode = "install-failed",
                        ErrorMessage = "The installation did not complete.",
                    },
                    WorkerChannelJson.Options),
            }));

        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Failed, command.Status);
        Assert.Equal("install-failed", command.ErrorCode);
    }

    [Fact]
    public async Task Report_WithoutCommandId_IsRejected()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();
        var request = StageReport(worker, credential, commandId, worker.DeploymentId);

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(() => host.RouteAsync(
            request with { Frame = request.Frame with { CommandId = null } }));

        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Queued, command.Status);
    }

    [Fact]
    public async Task Report_ForAnotherDeployment_IsRejected()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();

        await Assert.ThrowsAsync<WorkerProtocolValidationException>(
            () => host.RouteAsync(StageReport(worker, credential, commandId, Guid.NewGuid())));

        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Queued, command.Status);
    }

    [Fact]
    public async Task ReportRunning_AfterCostStopSupersededTheCommand_ThrowsTheProtocolConflict()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();
        await EnterCostStopAsync(host, worker);
        var command = await host.Store.DbContext.Set<WorkerCommand>().SingleAsync(q => q.PrimaryKey == commandId);
        command.Status = EWorkerCommandStatus.CancelledByCostStop;
        await host.Store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => host.RouteAsync(ProcessReport(
            EWorkerChannelFrameType.ReportRunning,
            worker,
            credential,
            commandId,
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "late",
                ProcessId = 1,
                ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            })));

        Assert.Equal(EWorkerCommandStatus.CancelledByCostStop, command.Status);
    }

    private static async Task<WorkerCommandClaimResponse?> ClaimAsync(
        WorkerChannelTestHost host,
        AuthenticatedWorker worker,
        string credential)
    {
        var result = await host.RouteAsync(ClaimRequest(worker, credential, worker.DeploymentId));
        return Assert.IsType<WorkerCommandClaimResponse>(result.Result);
    }

    private static WorkerChannelRequest ClaimRequest(
        AuthenticatedWorker worker,
        string credential,
        Guid deploymentId)
        => new(
            worker,
            credential,
            new WorkerChannelFrame
            {
                Type = EWorkerChannelFrameType.Claim,
                Id = Guid.NewGuid(),
                Payload = JsonSerializer.SerializeToElement(
                    new WorkerCommandClaimRequest { DeploymentId = deploymentId },
                    WorkerChannelJson.Options),
            });

    private static WorkerChannelRequest StageReport(
        AuthenticatedWorker worker,
        string credential,
        Guid commandId,
        Guid deploymentId)
        => new(
            worker,
            credential,
            new WorkerChannelFrame
            {
                Type = EWorkerChannelFrameType.ReportReceived,
                Id = Guid.NewGuid(),
                CommandId = commandId,
                Payload = JsonSerializer.SerializeToElement(
                    new WorkerCommandStageRequest { DeploymentId = deploymentId, Stage = "received" },
                    WorkerChannelJson.Options),
            });

    private static WorkerChannelRequest ProcessReport(
        EWorkerChannelFrameType type,
        AuthenticatedWorker worker,
        string credential,
        Guid commandId,
        WorkerCommandProcessRequest payload)
        => new(
            worker,
            credential,
            new WorkerChannelFrame
            {
                Type = type,
                Id = Guid.NewGuid(),
                CommandId = commandId,
                Payload = JsonSerializer.SerializeToElement(payload, WorkerChannelJson.Options),
            });

    private static async Task EnterCostStopAsync(WorkerChannelTestHost host, AuthenticatedWorker worker)
    {
        var deployment = await host.Store.DbContext.Set<GameServerDeployment>()
            .SingleAsync(q => q.PrimaryKey == worker.DeploymentId);
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.Terminating;
        await host.Store.DbContext.SaveChangesAsync();
    }

    private static WorkerChannelRequest HeartbeatRequest(
        AuthenticatedWorker worker,
        string credential,
        WorkerHeartbeatRequest payload)
        => new(
            worker,
            credential,
            new WorkerChannelFrame
            {
                Type = EWorkerChannelFrameType.Heartbeat,
                Id = Guid.NewGuid(),
                Payload = JsonSerializer.SerializeToElement(payload, WorkerChannelJson.Options),
            });

    private static Task<LifecycleWorker> ReadWorkerAsync(WorkerChannelTestHost host, AuthenticatedWorker worker)
        => host.Store.DbContext.Set<LifecycleWorker>()
            .AsNoTracking()
            .SingleAsync(q => q.PrimaryKey == worker.WorkerId);

    private static async Task AssertWorkerUnchangedAsync(
        WorkerChannelTestHost host,
        AuthenticatedWorker worker,
        LifecycleWorker before)
    {
        var after = await ReadWorkerAsync(host, worker);
        Assert.Equal(before.LastHeartbeatAt, after.LastHeartbeatAt);
        Assert.Equal(before.TimeStampUpdated, after.TimeStampUpdated);
        Assert.Equal(before.ProcessId, after.ProcessId);
        Assert.Equal(before.ProcessStartedAt, after.ProcessStartedAt);
        Assert.Equal(before.Status, after.Status);
    }
}
