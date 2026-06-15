using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class WorkerProtocolServiceTests
{
    [Fact]
    public async Task Register_ResponseLossCanRetryUntilPendingCredentialAuthenticates()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAsync(store, ELifecycleJobKind.Start, EDeploymentStatus.WaitingForWorker);

        var firstRegistration = await protocol.Service.RegisterAsync(
            protocol.Worker.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Identity = "worker-agent-1",
            },
            protocol.Bootstrap.BootstrapCredential);

        Assert.Equal(protocol.Worker.PrimaryKey, firstRegistration.Response.WorkerId);
        Assert.Equal(protocol.Deployment.PrimaryKey, firstRegistration.Response.DeploymentId);
        Assert.DoesNotContain(
            typeof(WorkerRegistrationResponse).GetProperties(),
            q => q.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
                 && q.PropertyType == typeof(string));
        Assert.Equal(EWorkerStatus.Online, protocol.Worker.Status);
        Assert.Equal(EDeploymentStatus.Preparing, protocol.Deployment.Status);
        Assert.NotNull(protocol.Worker.BootstrapCredentialConsumedAt);
        Assert.NotNull(protocol.Worker.BootstrapCredentialHash);
        Assert.Null(protocol.Worker.WorkerCredentialHash);
        Assert.NotEqual(firstRegistration.WorkerCredential, protocol.Worker.PendingWorkerCredentialHash);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.WorkerRegistered && q.WorkerFk == protocol.Worker.PrimaryKey);

        var pendingHashBeforeRejectedRetry = protocol.Worker.PendingWorkerCredentialHash;
        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => protocol.Service.RegisterAsync(
            protocol.Worker.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Identity = "different-worker-identity",
            },
            protocol.Bootstrap.BootstrapCredential));
        Assert.Equal(pendingHashBeforeRejectedRetry, protocol.Worker.PendingWorkerCredentialHash);

        var retryRegistration = await protocol.Service.RegisterAsync(
            protocol.Worker.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Identity = "worker-agent-1",
            },
            protocol.Bootstrap.BootstrapCredential);
        Assert.NotEqual(firstRegistration.WorkerCredential, retryRegistration.WorkerCredential);

        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            firstRegistration.WorkerCredential));
        await protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            retryRegistration.WorkerCredential);

        Assert.Null(protocol.Worker.BootstrapCredentialHash);
        Assert.Null(protocol.Worker.PendingWorkerCredentialHash);
        Assert.NotNull(protocol.Worker.WorkerCredentialHash);
        Assert.NotNull(protocol.Worker.WorkerCredentialActivatedAt);
        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => protocol.Service.RegisterAsync(
            protocol.Worker.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Identity = "worker-agent-1",
            },
            protocol.Bootstrap.BootstrapCredential));
    }

    [Fact]
    public async Task RotateCredential_KeepsCurrentValidUntilReplacementAuthenticates()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);
        await protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        var rotation = await protocol.Service.RotateCredentialAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCredentialRotationRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        Assert.NotEqual(rotation.WorkerCredential, protocol.Worker.PendingWorkerCredentialHash);
        Assert.NotNull(protocol.Worker.WorkerCredentialHash);
        await protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);
        var retryRotation = await protocol.Service.RotateCredentialAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCredentialRotationRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        Assert.NotEqual(rotation.WorkerCredential, retryRotation.WorkerCredential);
        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            rotation.WorkerCredential));
        await protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            retryRotation.WorkerCredential);

        Assert.Null(protocol.Worker.PendingWorkerCredentialHash);
        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential));
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.WorkerCredentialRotationIssued
                 && q.WorkerFk == protocol.Worker.PrimaryKey);
    }

    [Fact]
    public async Task Heartbeat_RequiresDeploymentBoundCredentialAndPersistsProcessEvidence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);
        var processStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        protocol.Worker.Status = EWorkerStatus.Degraded;
        await store.DbContext.SaveChangesAsync();

        await protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "waiting-for-command",
                ProcessId = 321,
                ProcessStartedAt = processStartedAt,
            },
            protocol.WorkerCredential);

        Assert.Equal(EWorkerStatus.Online, protocol.Worker.Status);
        Assert.Equal(321, protocol.Worker.ProcessId);
        Assert.Equal(Instant.FromDateTimeOffset(processStartedAt), protocol.Worker.ProcessStartedAt);
        Assert.NotNull(protocol.Worker.LastHeartbeatAt);

        await protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "runtime-absent",
            },
            protocol.WorkerCredential);
        Assert.Null(protocol.Worker.ProcessId);
        Assert.Null(protocol.Worker.ProcessStartedAt);

        await Assert.ThrowsAsync<WorkerProtocolAuthenticationException>(() => protocol.Service.HeartbeatAsync(
            protocol.Worker.PrimaryKey,
            new WorkerHeartbeatRequest
            {
                DeploymentId = Guid.NewGuid(),
            },
            protocol.WorkerCredential));
    }

    [Fact]
    public async Task ClaimCommand_RedeliversStableCommandWithoutDuplicatingAttempt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);

        var first = await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);
        var second = await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(protocol.Command.PrimaryKey, first.CommandId);
        Assert.Equal(first.CommandId, second.CommandId);
        Assert.Equal(1, first.Attempt);
        Assert.Equal(first.Attempt, second.Attempt);
        Assert.Equal("external-worker:" + protocol.Worker.PrimaryKey.ToString("N"), protocol.Command.LeaseOwner);
        Assert.Single(
            await store.DbContext.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.WorkerCommandUpdated
                            && q.WorkerCommandFk == protocol.Command.PrimaryKey)
                .ToArrayAsync());
    }

    [Fact]
    public async Task ClaimCommand_DoesNotLetPriorityReorderAcceptedNormalCommands()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);
        protocol.Command.Priority = 0;
        var laterCommand = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = protocol.Job,
            LifecycleJobFk = protocol.Job.PrimaryKey,
            Deployment = protocol.Deployment,
            DeploymentFk = protocol.Deployment.PrimaryKey,
            Worker = protocol.Worker,
            WorkerFk = protocol.Worker.PrimaryKey,
            Sequence = 2,
            Priority = int.MaxValue,
            ActionIdentifier = "later-command",
            Payload = "{}",
            Status = EWorkerCommandStatus.Queued,
            Stage = "queued",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        store.DbContext.WorkerCommands.Add(laterCommand);
        await store.DbContext.SaveChangesAsync();

        var claimed = await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        Assert.NotNull(claimed);
        Assert.Equal(protocol.Command.PrimaryKey, claimed.CommandId);
        Assert.Equal(1, claimed.Sequence);
    }

    [Fact]
    public async Task StartCommandOutcomes_UpdateRuntimeEvidenceButLeaveJobCompletionToControlPlane()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);
        await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);
        var processStartedAt = DateTimeOffset.UtcNow;

        await protocol.Service.ReportReceivedAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandStageRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "received",
            },
            protocol.WorkerCredential);
        await protocol.Service.ReportRunningAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "starting-game",
                ProcessId = 456,
                ProcessStartedAt = processStartedAt,
            },
            protocol.WorkerCredential);
        await protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "game-running",
                ProcessId = 456,
                ProcessStartedAt = processStartedAt,
            },
            protocol.WorkerCredential);

        Assert.Equal(EWorkerCommandStatus.Succeeded, protocol.Command.Status);
        Assert.Equal(ELifecycleJobStatus.Running, protocol.Job.Status);
        Assert.Equal("control-plane-test", protocol.Job.LeaseOwner);
        Assert.Equal(EDeploymentStatus.Running, protocol.Deployment.Status);
        Assert.Null(protocol.Deployment.TimeStampResolved);
        Assert.Equal(456, protocol.Worker.ProcessId);
        Assert.Equal(Instant.FromDateTimeOffset(processStartedAt), protocol.Worker.ProcessStartedAt);
        Assert.Equal(90, protocol.Job.Progress);

        var auditCount = await store.DbContext.HostingAuditRecords.CountAsync(q =>
            q.Kind == EHostingAuditEventKind.WorkerCommandSucceeded
            && q.WorkerCommandFk == protocol.Command.PrimaryKey);
        await protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "game-running",
                ProcessId = 456,
                ProcessStartedAt = processStartedAt,
            },
            protocol.WorkerCredential);
        Assert.Equal(
            auditCount,
            await store.DbContext.HostingAuditRecords.CountAsync(q =>
                q.Kind == EHostingAuditEventKind.WorkerCommandSucceeded
                && q.WorkerCommandFk == protocol.Command.PrimaryKey));
    }

    [Fact]
    public async Task StopSuccess_LeavesDeploymentUnresolvedForControlPlaneTeardown()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Stop, EDeploymentStatus.Running);
        protocol.Worker.ProcessId = 789;
        protocol.Worker.ProcessStartedAt = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(5);
        protocol.Deployment.IsProvisioningBlocked = true;
        protocol.Deployment.Status = EDeploymentStatus.Stopping;
        await store.DbContext.SaveChangesAsync();

        var claim = await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        Assert.NotNull(claim);

        await protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "game-stopped",
            },
            protocol.WorkerCredential);

        Assert.Equal(EWorkerCommandStatus.Succeeded, protocol.Command.Status);
        Assert.Equal(ELifecycleJobStatus.Running, protocol.Job.Status);
        Assert.Equal(EDeploymentStatus.Stopping, protocol.Deployment.Status);
        Assert.Null(protocol.Deployment.TimeStampResolved);
        Assert.Null(protocol.Worker.ProcessId);
        Assert.Null(protocol.Worker.ProcessStartedAt);
    }

    [Fact]
    public async Task FaultStop_IsTheOnlyWorkerCommandAllowedThroughFaultTerminationBarrier()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Terminate);
        protocol.Command.ActionIdentifier = GameServerActionIds.FaultStop;
        protocol.Command.Payload = FaultPayload(DateTimeOffset.UtcNow.AddMinutes(1));
        protocol.Worker.ProcessId = 1234;
        protocol.Worker.ProcessStartedAt = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(2);
        protocol.Deployment.IsProvisioningBlocked = true;
        protocol.Deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        await store.DbContext.SaveChangesAsync();

        var claimed = await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);

        Assert.NotNull(claimed);
        Assert.Equal(GameServerActionIds.FaultStop, claimed.ActionIdentifier);
        await protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "fault-evidence-preserved",
                Output = "bounded diagnostics",
            },
            protocol.WorkerCredential);

        Assert.Equal(EWorkerCommandStatus.Succeeded, protocol.Command.Status);
        Assert.Equal(EDeploymentStatus.TerminatingDueToFault, protocol.Deployment.Status);
        Assert.True(protocol.Deployment.IsProvisioningBlocked);
        Assert.Null(protocol.Worker.ProcessId);
        Assert.Null(protocol.Worker.ProcessStartedAt);
        Assert.Equal("bounded diagnostics", protocol.Command.DiagnosticOutput);
    }

    [Fact]
    public async Task FaultStop_RejectsAnExpiredDurableEvidenceDeadline()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Terminate);
        protocol.Command.ActionIdentifier = GameServerActionIds.FaultStop;
        protocol.Command.Payload = FaultPayload(DateTimeOffset.UtcNow.AddMinutes(-1));
        protocol.Deployment.IsProvisioningBlocked = true;
        protocol.Deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        await store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential));

        Assert.Equal(EWorkerCommandStatus.Queued, protocol.Command.Status);
    }

    [Fact]
    public async Task FaultStop_RejectsAReportAfterTheDurableEvidenceDeadline()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Terminate);
        protocol.Command.ActionIdentifier = GameServerActionIds.FaultStop;
        protocol.Command.Payload = FaultPayload(DateTimeOffset.UtcNow.AddMinutes(1));
        protocol.Deployment.IsProvisioningBlocked = true;
        protocol.Deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        await store.DbContext.SaveChangesAsync();
        await protocol.Service.ClaimCommandAsync(
            protocol.Worker.PrimaryKey,
            new WorkerCommandClaimRequest { DeploymentId = protocol.Deployment.PrimaryKey },
            protocol.WorkerCredential);
        await protocol.Service.ReportReceivedAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandStageRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "fault-evidence-received",
            },
            protocol.WorkerCredential);
        protocol.Command.Payload = FaultPayload(DateTimeOffset.UtcNow.AddMinutes(-1));
        await store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "late-fault-evidence",
            },
            protocol.WorkerCredential));

        Assert.Equal(EWorkerCommandStatus.Received, protocol.Command.Status);
    }

    [Fact]
    public async Task TerminationBarrier_RejectsLateSuccessWithoutResurrectingDeployment()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);
        var priorStartedAt = protocol.Deployment.TimeStampStarted;
        protocol.Deployment.IsProvisioningBlocked = true;
        protocol.Deployment.Status = EDeploymentStatus.Terminating;
        await store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkerProtocolConflictException>(() => protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "too-late",
                ProcessId = 987,
                ProcessStartedAt = DateTimeOffset.UtcNow,
            },
            protocol.WorkerCredential));

        Assert.Equal(EWorkerCommandStatus.Queued, protocol.Command.Status);
        Assert.Equal(EDeploymentStatus.Terminating, protocol.Deployment.Status);
        Assert.Equal(priorStartedAt, protocol.Deployment.TimeStampStarted);
    }

    [Fact]
    public async Task FailureReport_SanitizesSecretsAndKeepsJobLeaseAuthoritative()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.Start);

        await protocol.Service.ReportFailedAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandFailureRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "prepare-failed",
                ErrorCode = "worker-error",
                ErrorMessage = "token=super-secret-value failed",
                Output = "failure diagnostics token=super-secret-output",
            },
            protocol.WorkerCredential);

        Assert.Equal(EWorkerCommandStatus.Failed, protocol.Command.Status);
        Assert.Equal(ELifecycleJobStatus.Running, protocol.Job.Status);
        Assert.Equal("control-plane-test", protocol.Job.LeaseOwner);
        Assert.DoesNotContain("super-secret-value", protocol.Command.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("<redacted>", protocol.Command.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-output", protocol.Command.DiagnosticOutput, StringComparison.Ordinal);
        Assert.Contains("<redacted>", protocol.Command.DiagnosticOutput, StringComparison.Ordinal);
        Assert.Equal("worker-command-failed", protocol.Deployment.FaultCode);
        Assert.Null(protocol.Deployment.TimeStampResolved);
    }

    [Fact]
    public async Task CollectLogsSuccess_PersistsBoundedRedactedOutputButAuditsOnlyMetadata()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var protocol = await SeedAndRegisterAsync(store, ELifecycleJobKind.CollectLogs);
        var output = "collected-log-prefix token=diagnostic-secret\n" + new string('x', 20_000);

        await protocol.Service.ReportSucceededAsync(
            protocol.Worker.PrimaryKey,
            protocol.Command.PrimaryKey,
            new WorkerCommandProcessRequest
            {
                DeploymentId = protocol.Deployment.PrimaryKey,
                Stage = "logs-collected",
                Output = output,
            },
            protocol.WorkerCredential);

        Assert.NotNull(protocol.Command.DiagnosticOutput);
        Assert.True(protocol.Command.DiagnosticOutput.Length <= 16_384);
        Assert.Contains("collected-log-prefix", protocol.Command.DiagnosticOutput, StringComparison.Ordinal);
        Assert.Contains("<redacted>", protocol.Command.DiagnosticOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic-secret", protocol.Command.DiagnosticOutput, StringComparison.Ordinal);
        var audit = await store.DbContext.HostingAuditRecords.SingleAsync(q =>
            q.Kind == EHostingAuditEventKind.WorkerCommandSucceeded
            && q.WorkerCommandFk == protocol.Command.PrimaryKey);
        Assert.Contains("DiagnosticOutputPresent=True", audit.Details, StringComparison.Ordinal);
        Assert.Contains($"DiagnosticOutputLength={protocol.Command.DiagnosticOutput.Length}", audit.Details);
        Assert.DoesNotContain("collected-log-prefix", audit.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic-secret", audit.Details, StringComparison.Ordinal);
    }

    private static string FaultPayload(DateTimeOffset deadline)
        => JsonSerializer.Serialize(new WorkerCommandPayload { FaultEvidenceDeadline = deadline });

    private static async Task<RegisteredProtocolSeed> SeedAndRegisterAsync(
        HostingTestStore store,
        ELifecycleJobKind kind,
        EDeploymentStatus deploymentStatus = EDeploymentStatus.Preparing)
    {
        var seed = await SeedAsync(store, kind, deploymentStatus);
        var registration = await seed.Service.RegisterAsync(
            seed.Worker.PrimaryKey,
            new WorkerRegistrationRequest
            {
                DeploymentId = seed.Deployment.PrimaryKey,
                Identity = "external-test-worker",
            },
            seed.Bootstrap.BootstrapCredential);
        return new RegisteredProtocolSeed(
            seed.Service,
            seed.Deployment,
            seed.Worker,
            seed.Job,
            seed.Command,
            registration.WorkerCredential);
    }

    private static async Task<ProtocolSeed> SeedAsync(
        HostingTestStore store,
        ELifecycleJobKind kind,
        EDeploymentStatus deploymentStatus)
    {
        var hosting = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: deploymentStatus);
        var deployment = hosting.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            Identity = "pending",
            Status = EWorkerStatus.Provisioning,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = hosting.Server.PrimaryKey,
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = hosting.User.PrimaryKey,
            Kind = kind,
            ActionIdentifier = kind.ToString().ToLowerInvariant(),
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "waiting-for-worker",
            Progress = 20,
            Attempt = 1,
            MaxAttempts = 3,
            LeaseOwner = "control-plane-test",
            LeaseExpires = now + Duration.FromMinutes(1),
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStarted = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = job,
            LifecycleJobFk = job.PrimaryKey,
            Deployment = deployment,
            DeploymentFk = deployment.PrimaryKey,
            Worker = worker,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            ActionIdentifier = job.ActionIdentifier,
            Payload = "{\"test\":true}",
            Status = EWorkerCommandStatus.Queued,
            Stage = "queued",
            TimeStampCreated = now,
        };
        var options = Options.Create(new LifecycleWorkerOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(10),
            JobLease = TimeSpan.FromSeconds(45),
            BootstrapCredentialLifetime = TimeSpan.FromMinutes(10),
            RegistrationGrace = TimeSpan.FromMinutes(2),
            WorkerCredentialLifetime = TimeSpan.FromHours(1),
        });
        var credentials = new WorkerCredentialService(options);
        var bootstrap = credentials.IssueBootstrapCredential(worker);
        store.DbContext.AddRange(worker, job, command);
        await store.DbContext.SaveChangesAsync();
        var service = new WorkerProtocolService(
            store.DbContext,
            credentials,
            new HostingAuditWriter(),
            options);
        return new ProtocolSeed(service, deployment, worker, job, command, bootstrap);
    }

    private sealed record ProtocolSeed(
        WorkerProtocolService Service,
        GameServerDeployment Deployment,
        LifecycleWorker Worker,
        LifecycleJob Job,
        WorkerCommand Command,
        WorkerBootstrapConfiguration Bootstrap);

    private sealed record RegisteredProtocolSeed(
        WorkerProtocolService Service,
        GameServerDeployment Deployment,
        LifecycleWorker Worker,
        LifecycleJob Job,
        WorkerCommand Command,
        string WorkerCredential);
}
