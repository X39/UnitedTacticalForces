using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public interface ICloudWorkerApiClient
{
    Task<WorkerRegistrationResult> RegisterAsync(
        CloudWorkerBootstrapConfig bootstrap,
        WorkerRegistrationRequest request,
        CancellationToken cancellationToken);

    Task<WorkerCredentialRotationResult> RotateCredentialAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        CancellationToken cancellationToken);

    Task HeartbeatAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        WorkerHeartbeatRequest request,
        CancellationToken cancellationToken);

    Task<WorkerCommandClaimResponse?> ClaimNextAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        CancellationToken cancellationToken);

    /// <returns>
    /// <see langword="true"/> when the control plane accepted the transition; otherwise
    /// <see langword="false"/> when cost-stop or another durable barrier superseded the command.
    /// </returns>
    Task<bool> ReportCommandAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        Guid commandId,
        WorkerCommandReportRequest request,
        CancellationToken cancellationToken);
}