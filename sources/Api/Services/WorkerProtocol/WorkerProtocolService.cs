using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

[Scoped<WorkerProtocolService, IWorkerProtocolService>]
internal sealed class WorkerProtocolService(
    DatabaseContext dbContext,
    IWorkerCredentialService credentialService,
    IHostingAuditWriter auditWriter,
    IOptions<LifecycleWorkerOptions> options) : IWorkerProtocolService
{
    private static readonly JsonSerializerOptions CommandJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LifecycleWorkerOptions _options = options.Value;

    public async Task<WorkerRegistrationResult> RegisterAsync(
        Guid workerId,
        WorkerRegistrationRequest request,
        string bootstrapCredential,
        CancellationToken cancellationToken = default)
    {
        var identity = request.Identity.Trim();
        if (request.DeploymentId == Guid.Empty || string.IsNullOrWhiteSpace(identity) || identity.Length > 256)
            throw new WorkerProtocolValidationException("A deployment id and bounded worker identity are required.");

        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var worker = await LockWorkerAsync(workerId, request.DeploymentId, cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (worker is null
            || worker.DeploymentFk != request.DeploymentId
            || worker.TimeStampStopped is not null
            || worker.Deployment?.TimeStampResolved is not null
            || !credentialService.VerifyBootstrapCredential(worker, bootstrapCredential, now)
            || (worker.BootstrapCredentialConsumedAt is not null
                && (worker.PendingWorkerCredentialHash is null
                    || !string.Equals(worker.Identity, identity, StringComparison.Ordinal))))
        {
            throw new WorkerProtocolAuthenticationException();
        }

        if (worker.Deployment!.Status is EDeploymentStatus.Terminating
                or EDeploymentStatus.TerminatingDueToFault
                or EDeploymentStatus.Stopped
                or EDeploymentStatus.Terminated
                or EDeploymentStatus.TerminatedDueToFault)
        {
            throw new WorkerProtocolConflictException("The deployment no longer accepts worker registration.");
        }

        var isRegistrationRetry = worker.BootstrapCredentialConsumedAt is not null;
        worker.BootstrapCredentialConsumedAt ??= now;
        worker.Identity = identity;
        worker.Status = EWorkerStatus.Online;
        worker.LastHeartbeatAt = now;
        worker.TimeStampRegistered ??= now;
        worker.TimeStampUpdated = now;
        var issuedCredential = credentialService.IssuePendingWorkerCredential(worker, now);

        if (worker.Deployment.Status is EDeploymentStatus.Provisioning or EDeploymentStatus.WaitingForWorker)
            worker.Deployment.Status = EDeploymentStatus.Preparing;
        worker.Deployment.Stage = "worker-online";
        worker.Deployment.TimeStampUpdated = now;

        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.WorkerRegistered,
            isRegistrationRetry
                ? "External worker registration was retried with its unexpired bootstrap credential."
                : "External worker registered with a deployment-scoped credential.",
            serverId: worker.Deployment.GameServerFk,
            deploymentId: worker.DeploymentFk,
            workerId: worker.PrimaryKey,
            details: $"Identity={identity}; Retry={isRegistrationRetry}; "
                     + $"CredentialExpiresAt={issuedCredential.ExpiresAt:O}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new WorkerRegistrationResult(
            new WorkerRegistrationResponse
            {
                WorkerId = worker.PrimaryKey,
                DeploymentId = worker.DeploymentFk,
                CredentialExpiresAt = issuedCredential.ExpiresAt,
                HeartbeatIntervalSeconds = PositiveSeconds(
                    _options.HeartbeatInterval,
                    nameof(LifecycleWorkerOptions.HeartbeatInterval)),
            },
            issuedCredential.Credential);
    }

    public async Task<WorkerCredentialRotationResult> RotateCredentialAsync(
        Guid workerId,
        WorkerCredentialRotationRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        if (request.DeploymentId == Guid.Empty)
            throw new WorkerProtocolValidationException("A deployment id is required.");

        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = await AuthenticateWorkerAsync(
                workerId,
                request.DeploymentId,
                workerCredential,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        var issuedCredential = credentialService.IssuePendingWorkerCredential(worker, now);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.WorkerCredentialRotationIssued,
            "External worker requested a replacement deployment-scoped credential.",
            serverId: worker.Deployment!.GameServerFk,
            deploymentId: worker.DeploymentFk,
            workerId: worker.PrimaryKey,
            details: $"CredentialExpiresAt={issuedCredential.ExpiresAt:O}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new WorkerCredentialRotationResult(
            new WorkerCredentialRotationResponse
            {
                WorkerId = worker.PrimaryKey,
                DeploymentId = worker.DeploymentFk,
                CredentialExpiresAt = issuedCredential.ExpiresAt,
            },
            issuedCredential.Credential);
    }

    public async Task HeartbeatAsync(
        Guid workerId,
        WorkerHeartbeatRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        ValidateProcessEvidence(request.ProcessId, request.ProcessStartedAt);
        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = await AuthenticateWorkerAsync(
                workerId,
                request.DeploymentId,
                workerCredential,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        var recovered = worker.Status is EWorkerStatus.Provisioning or EWorkerStatus.Degraded;
        if (recovered)
            worker.Status = EWorkerStatus.Online;
        worker.LastHeartbeatAt = now;
        worker.TimeStampUpdated = now;
        if (request.ProcessId is not null)
        {
            worker.ProcessId = request.ProcessId;
            worker.ProcessStartedAt = Instant.FromDateTimeOffset(request.ProcessStartedAt!.Value);
        }
        else
        {
            worker.ProcessId = null;
            worker.ProcessStartedAt = null;
        }

        auditWriter.Add(
            dbContext,
            recovered ? EHostingAuditEventKind.WorkerRecovered : EHostingAuditEventKind.WorkerHeartbeat,
            recovered ? "External worker communication recovered." : "External worker heartbeat received.",
            serverId: worker.Deployment!.GameServerFk,
            deploymentId: worker.DeploymentFk,
            workerId: worker.PrimaryKey,
            details: string.IsNullOrWhiteSpace(request.Stage) ? null : $"Stage={BoundStage(request.Stage)}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkerCommandClaimResponse?> ClaimCommandAsync(
        Guid workerId,
        WorkerCommandClaimRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = await AuthenticateWorkerAsync(
                workerId,
                request.DeploymentId,
                workerCredential,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        var command = await dbContext.WorkerCommands
            .Include(q => q.LifecycleJob)
            .Where(q => q.WorkerFk == worker.PrimaryKey
                        && q.DeploymentFk == worker.DeploymentFk
                        && (q.Status == EWorkerCommandStatus.Queued
                            || q.Status == EWorkerCommandStatus.Received
                            || q.Status == EWorkerCommandStatus.Running)
                        && q.LifecycleJob != null
                        && (q.LifecycleJob.Status == ELifecycleJobStatus.Running
                            || q.LifecycleJob.Status == ELifecycleJobStatus.WaitingForWorker))
            .OrderBy(q => q.Status == EWorkerCommandStatus.Received || q.Status == EWorkerCommandStatus.Running
                ? 0
                : q.LeaseExpires != null && q.LeaseExpires > now ? 1 : 2)
            // An already-delivered command is always redelivered first. Once it is terminal, accepted
            // normal commands execute strictly in request order; priority must not reorder them.
            .ThenBy(q => q.Sequence)
            .ThenByDescending(q => q.Priority)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (command is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        if (!CanExecuteCommand(worker.Deployment!, command, now))
            throw new WorkerProtocolConflictException("The command was superseded by deployment cost-stop state.");

        var newDeliveryAttempt = command.LeaseExpires is null || command.LeaseExpires <= now;
        var lease = PositiveDuration(_options.JobLease, nameof(LifecycleWorkerOptions.JobLease));
        if (newDeliveryAttempt)
            command.Attempt++;
        command.LeaseOwner = WorkerLeaseOwner(worker.PrimaryKey);
        command.LeaseExpires = now + lease;
        worker.LastHeartbeatAt = now;
        if (worker.Status is EWorkerStatus.Provisioning or EWorkerStatus.Degraded)
            worker.Status = EWorkerStatus.Online;
        worker.TimeStampUpdated = now;
        command.LifecycleJob!.Stage = "worker-command-claimed";
        command.LifecycleJob.TimeStampUpdated = now;

        if (newDeliveryAttempt)
        {
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.WorkerCommandUpdated,
                "External worker claimed a stable command.",
                serverId: command.LifecycleJob.GameServerFk,
                deploymentId: command.DeploymentFk,
                lifecycleJobId: command.LifecycleJobFk,
                workerId: worker.PrimaryKey,
                workerCommandId: command.PrimaryKey,
                details: $"Attempt={command.Attempt}; Action={command.ActionIdentifier}");
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new WorkerCommandClaimResponse
        {
            CommandId = command.PrimaryKey,
            LifecycleJobId = command.LifecycleJobFk,
            DeploymentId = command.DeploymentFk,
            WorkerId = command.WorkerFk,
            Sequence = command.Sequence,
            ActionIdentifier = command.ActionIdentifier,
            Payload = command.Payload,
            Attempt = command.Attempt,
            LeaseExpiresAt = command.LeaseExpires.Value.ToDateTimeOffset(),
        };
    }

    public Task ReportReceivedAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandStageRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
        => UpdateCommandAsync(
            workerId,
            commandId,
            request.DeploymentId,
            request.Stage,
            workerCredential,
            EWorkerCommandStatus.Received,
            null,
            null,
            null,
            null,
            null,
            cancellationToken);

    public Task ReportRunningAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandProcessRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        ValidateProcessEvidence(request.ProcessId, request.ProcessStartedAt);
        return UpdateCommandAsync(
            workerId,
            commandId,
            request.DeploymentId,
            request.Stage,
            workerCredential,
            EWorkerCommandStatus.Running,
            request.ProcessId,
            request.ProcessStartedAt,
            null,
            null,
            null,
            cancellationToken);
    }

    public Task ReportSucceededAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandProcessRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        ValidateProcessEvidence(request.ProcessId, request.ProcessStartedAt);
        return UpdateCommandAsync(
            workerId,
            commandId,
            request.DeploymentId,
            request.Stage,
            workerCredential,
            EWorkerCommandStatus.Succeeded,
            request.ProcessId,
            request.ProcessStartedAt,
            null,
            null,
            request.Output,
            cancellationToken);
    }

    public Task ReportFailedAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandFailureRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ErrorCode) || string.IsNullOrWhiteSpace(request.ErrorMessage))
            throw new WorkerProtocolValidationException("A bounded error code and message are required.");
        return UpdateCommandAsync(
            workerId,
            commandId,
            request.DeploymentId,
            request.Stage,
            workerCredential,
            EWorkerCommandStatus.Failed,
            null,
            null,
            request.ErrorCode,
            request.ErrorMessage,
            request.Output,
            cancellationToken);
    }

    private async Task UpdateCommandAsync(
        Guid workerId,
        Guid commandId,
        Guid deploymentId,
        string stage,
        string workerCredential,
        EWorkerCommandStatus targetStatus,
        int? processId,
        DateTimeOffset? processStartedAt,
        string? errorCode,
        string? errorMessage,
        string? diagnosticOutput,
        CancellationToken cancellationToken)
    {
        stage = BoundStage(stage);
        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = await AuthenticateWorkerAsync(
                workerId,
                deploymentId,
                workerCredential,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        var command = await dbContext.WorkerCommands
            .Include(q => q.LifecycleJob)
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == commandId
                     && q.WorkerFk == worker.PrimaryKey
                     && q.DeploymentFk == worker.DeploymentFk,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new WorkerProtocolNotFoundException("The assigned worker command does not exist.");
        var deployment = worker.Deployment!;
        if (!CanExecuteCommand(deployment, command, now)
            || command.Status is EWorkerCommandStatus.Retried
                or EWorkerCommandStatus.Skipped
                or EWorkerCommandStatus.CancelledByCostStop
            || command.LifecycleJob?.Status is ELifecycleJobStatus.Skipped
                or ELifecycleJobStatus.CancelledByCostStop)
        {
            throw new WorkerProtocolConflictException("The worker command was superseded by control-plane state.");
        }
        if (targetStatus is EWorkerCommandStatus.Succeeded
            && command.LifecycleJob?.Kind is ELifecycleJobKind.Start or ELifecycleJobKind.Restart
            && !IsFaultStop(command.ActionIdentifier)
            && processId is null)
        {
            throw new WorkerProtocolValidationException(
                "A successful process-starting command requires process identity evidence.");
        }

        var changed = ApplyCommandTransition(
            command,
            targetStatus,
            stage,
            now,
            errorCode,
            errorMessage,
            diagnosticOutput);
        worker.LastHeartbeatAt = now;
        if (worker.Status is EWorkerStatus.Provisioning or EWorkerStatus.Degraded)
            worker.Status = EWorkerStatus.Online;
        worker.TimeStampUpdated = now;
        ApplyProcessEvidence(
            worker,
            command.LifecycleJob?.Kind,
            command.ActionIdentifier,
            targetStatus,
            processId,
            processStartedAt);
        if (changed)
        {
            ApplyLifecycleEvidence(command, deployment, targetStatus, stage, now);
            auditWriter.Add(
                dbContext,
                AuditKind(targetStatus),
                AuditSummary(targetStatus),
                serverId: command.LifecycleJob?.GameServerFk ?? deployment.GameServerFk,
                deploymentId: command.DeploymentFk,
                lifecycleJobId: command.LifecycleJobFk,
                workerId: worker.PrimaryKey,
                workerCommandId: command.PrimaryKey,
                details: BuildCommandAuditDetails(command, stage, targetStatus));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool CanExecuteCommand(
        GameServerDeployment deployment,
        WorkerCommand command,
        Instant now)
    {
        if (IsFaultStop(command.ActionIdentifier))
        {
            return deployment.IsProvisioningBlocked
                   && deployment.Status is EDeploymentStatus.TerminatingDueToFault
                   && deployment.TimeStampResolved is null
                   && HasUnexpiredFaultEvidenceDeadline(command.Payload, now);
        }
        if (command.LifecycleJob?.Kind is ELifecycleJobKind.Stop
            && deployment.Status is EDeploymentStatus.Stopping or EDeploymentStatus.PreservationFailed)
        {
            return true;
        }
        return !deployment.IsProvisioningBlocked
               && deployment.Status is not (EDeploymentStatus.Stopping
                   or EDeploymentStatus.Terminating
                   or EDeploymentStatus.TerminatingDueToFault
                   or EDeploymentStatus.Stopped
                   or EDeploymentStatus.Terminated
                or EDeploymentStatus.TerminatedDueToFault);
    }

    private static bool HasUnexpiredFaultEvidenceDeadline(string payload, Instant now)
    {
        try
        {
            var commandPayload = JsonSerializer.Deserialize<WorkerCommandPayload>(payload, CommandJsonOptions);
            return commandPayload?.FaultEvidenceDeadline is { } deadline
                   && Instant.FromDateTimeOffset(deadline) > now;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ApplyCommandTransition(
        WorkerCommand command,
        EWorkerCommandStatus targetStatus,
        string stage,
        Instant now,
        string? errorCode,
        string? errorMessage,
        string? diagnosticOutput)
    {
        if (command.Status == targetStatus)
            return false;
        if (targetStatus is EWorkerCommandStatus.Received
            && command.Status is EWorkerCommandStatus.Running
                or EWorkerCommandStatus.Succeeded
                or EWorkerCommandStatus.Failed)
        {
            return false;
        }
        if (targetStatus is EWorkerCommandStatus.Running
            && command.Status is EWorkerCommandStatus.Succeeded or EWorkerCommandStatus.Failed)
        {
            return false;
        }
        if (command.Status is EWorkerCommandStatus.Succeeded or EWorkerCommandStatus.Failed)
        {
            throw new WorkerProtocolConflictException(
                $"The worker command already has terminal status {command.Status}.");
        }

        command.Status = targetStatus;
        command.Stage = stage;
        switch (targetStatus)
        {
            case EWorkerCommandStatus.Received:
                command.TimeStampReceived ??= now;
                break;
            case EWorkerCommandStatus.Running:
                command.TimeStampReceived ??= now;
                command.TimeStampStarted ??= now;
                if (command.Attempt == 0)
                    command.Attempt = 1;
                break;
            case EWorkerCommandStatus.Succeeded:
                command.TimeStampReceived ??= now;
                command.TimeStampStarted ??= now;
                command.TimeStampCompleted = now;
                command.ErrorCode = null;
                command.ErrorMessage = null;
                command.DiagnosticOutput = SanitizeDiagnosticOutput(diagnosticOutput);
                command.LeaseOwner = null;
                command.LeaseExpires = null;
                break;
            case EWorkerCommandStatus.Failed:
                command.TimeStampReceived ??= now;
                command.TimeStampStarted ??= now;
                command.TimeStampCompleted = now;
                command.ErrorCode = HostingFailureSanitizer.Sanitize(errorCode, 128);
                command.ErrorMessage = HostingFailureSanitizer.Sanitize(errorMessage, 2048);
                command.DiagnosticOutput = SanitizeDiagnosticOutput(diagnosticOutput);
                command.LeaseOwner = null;
                command.LeaseExpires = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, null);
        }
        return true;
    }

    private static string? SanitizeDiagnosticOutput(string? output)
        => string.IsNullOrWhiteSpace(output)
            ? null
            : HostingFailureSanitizer.Sanitize(output, 16384);

    private static string BuildCommandAuditDetails(
        WorkerCommand command,
        string stage,
        EWorkerCommandStatus status)
    {
        var diagnosticMetadata = $"DiagnosticOutputPresent={command.DiagnosticOutput is not null}; "
                                 + $"DiagnosticOutputLength={command.DiagnosticOutput?.Length ?? 0}";
        return status is EWorkerCommandStatus.Failed
            ? $"Error={command.ErrorMessage}; {diagnosticMetadata}"
            : $"Stage={stage}; Action={command.ActionIdentifier}; {diagnosticMetadata}";
    }

    private static void ApplyProcessEvidence(
        LifecycleWorker worker,
        ELifecycleJobKind? kind,
        string actionIdentifier,
        EWorkerCommandStatus status,
        int? processId,
        DateTimeOffset? processStartedAt)
    {
        if (status is EWorkerCommandStatus.Succeeded
            && (kind is ELifecycleJobKind.Stop || IsFaultStop(actionIdentifier)))
        {
            worker.ProcessId = null;
            worker.ProcessStartedAt = null;
            return;
        }
        if (processId is null)
            return;
        worker.ProcessId = processId;
        worker.ProcessStartedAt = Instant.FromDateTimeOffset(processStartedAt!.Value);
    }

    private static void ApplyLifecycleEvidence(
        WorkerCommand command,
        GameServerDeployment deployment,
        EWorkerCommandStatus status,
        string stage,
        Instant now)
    {
        var job = command.LifecycleJob;
        if (job is not null)
        {
            job.Stage = stage;
            job.TimeStampUpdated = now;
            if (status is EWorkerCommandStatus.Received)
                job.Progress = Math.Max(job.Progress, 30);
            else if (status is EWorkerCommandStatus.Running)
                job.Progress = Math.Max(job.Progress, 40);
            else if (status is EWorkerCommandStatus.Succeeded)
                job.Progress = Math.Max(job.Progress, 90);
        }

        if (IsFaultStop(command.ActionIdentifier))
        {
            deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        }
        else if (job?.Kind is ELifecycleJobKind.Start)
        {
            deployment.Status = status is EWorkerCommandStatus.Succeeded
                ? EDeploymentStatus.Running
                : EDeploymentStatus.Preparing;
            if (status is EWorkerCommandStatus.Succeeded)
                deployment.TimeStampStarted ??= now;
        }
        else if (job?.Kind is ELifecycleJobKind.Stop)
        {
            deployment.Status = EDeploymentStatus.Stopping;
        }
        deployment.Stage = stage;
        deployment.TimeStampUpdated = now;
        if (!IsFaultStop(command.ActionIdentifier)
            && status is EWorkerCommandStatus.Failed
            && job?.Kind is ELifecycleJobKind.Start or ELifecycleJobKind.Stop)
        {
            deployment.FaultCode = "worker-command-failed";
            deployment.FaultMessage = command.ErrorMessage;
            deployment.TimeStampFaulted ??= now;
        }
    }

    private static bool IsFaultStop(string actionIdentifier)
        => string.Equals(
            actionIdentifier,
            GameServerActionIds.FaultStop,
            StringComparison.OrdinalIgnoreCase);

    private async Task<LifecycleWorker> AuthenticateWorkerAsync(
        Guid workerId,
        Guid deploymentId,
        string workerCredential,
        Instant now,
        CancellationToken cancellationToken)
    {
        var worker = await LockWorkerAsync(workerId, deploymentId, cancellationToken).ConfigureAwait(false);
        if (worker is null
            || worker.DeploymentFk != deploymentId
            || worker.TimeStampStopped is not null
            || worker.Deployment?.TimeStampResolved is not null
            || !credentialService.VerifyAndPromoteWorkerCredential(worker, workerCredential, now))
        {
            throw new WorkerProtocolAuthenticationException();
        }
        return worker;
    }

    private async Task<LifecycleWorker?> LockWorkerAsync(
        Guid workerId,
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        if (!IsPostgreSql)
        {
            return await dbContext.LifecycleWorkers
                .Include(q => q.Deployment)
                .SingleOrDefaultAsync(
                    q => q.PrimaryKey == workerId && q.DeploymentFk == deploymentId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var serverId = await dbContext.LifecycleWorkers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == workerId && q.DeploymentFk == deploymentId)
            .Select(q => (Guid?) q.Deployment!.GameServerFk)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (serverId is null)
            return null;

        // Match lifecycle execution's server -> deployment lock order. A late worker
        // outcome can therefore never overwrite a concurrently persisted Terminate barrier.
        await dbContext.GameServers
            .FromSqlInterpolated($"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {serverId.Value} FOR UPDATE""")
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        var deployment = await dbContext.GameServerDeployments
            .FromSqlInterpolated(
                $"""SELECT * FROM "GameServerDeployments" WHERE "PrimaryKey" = {deploymentId} FOR UPDATE""")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        var worker = await dbContext.LifecycleWorkers
            .FromSqlInterpolated(
                $"""SELECT * FROM "LifecycleWorkers" WHERE "PrimaryKey" = {workerId} FOR UPDATE""")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        worker.Deployment = deployment;
        return worker;
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
        => dbContext.Database.BeginTransactionAsync(
            IsPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
            cancellationToken);

    private bool IsPostgreSql
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static string BoundStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
            throw new WorkerProtocolValidationException("A command stage is required.");
        return HostingFailureSanitizer.Sanitize(stage.Trim(), 128);
    }

    private static void ValidateProcessEvidence(int? processId, DateTimeOffset? processStartedAt)
    {
        if ((processId is null) != (processStartedAt is null) || processId is <= 0)
        {
            throw new WorkerProtocolValidationException(
                "Process id and process start time must either both be supplied or both be omitted.");
        }
    }

    private static int PositiveSeconds(TimeSpan value, string optionName)
    {
        var duration = PositiveDuration(value, optionName);
        return Math.Max(1, checked((int) Math.Ceiling(duration.TotalSeconds)));
    }

    private static Duration PositiveDuration(TimeSpan value, string optionName)
    {
        if (value <= TimeSpan.Zero)
            throw new InvalidOperationException($"{optionName} must be greater than zero.");
        return Duration.FromTimeSpan(value);
    }

    private static string WorkerLeaseOwner(Guid workerId) => $"external-worker:{workerId:N}";

    private static EHostingAuditEventKind AuditKind(EWorkerCommandStatus status)
        => status switch
        {
            EWorkerCommandStatus.Succeeded => EHostingAuditEventKind.WorkerCommandSucceeded,
            EWorkerCommandStatus.Failed => EHostingAuditEventKind.WorkerCommandFailed,
            _ => EHostingAuditEventKind.WorkerCommandUpdated,
        };

    private static string AuditSummary(EWorkerCommandStatus status)
        => status switch
        {
            EWorkerCommandStatus.Received => "External worker received a command.",
            EWorkerCommandStatus.Running => "External worker started a command.",
            EWorkerCommandStatus.Succeeded => "External worker completed a command.",
            EWorkerCommandStatus.Failed => "External worker reported a command failure.",
            _ => "External worker updated a command.",
        };
}
