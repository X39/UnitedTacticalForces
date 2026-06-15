using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

public interface IWorkerProtocolService
{
    Task<WorkerRegistrationResult> RegisterAsync(
        Guid workerId,
        WorkerRegistrationRequest request,
        string bootstrapCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerCredentialRotationResult> RotateCredentialAsync(
        Guid workerId,
        WorkerCredentialRotationRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task HeartbeatAsync(
        Guid workerId,
        WorkerHeartbeatRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerCommandClaimResponse?> ClaimCommandAsync(
        Guid workerId,
        WorkerCommandClaimRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportReceivedAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandStageRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportRunningAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandProcessRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportSucceededAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandProcessRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportFailedAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandFailureRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);
}
