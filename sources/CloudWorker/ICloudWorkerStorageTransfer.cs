using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public interface ICloudWorkerStorageTransfer
{
    Task ResetPersistentOverlayAsync(
        string deploymentRoot,
        CancellationToken cancellationToken);

    Task ApplyPersistentSnapshotAsync(
        WorkerCommandClaimResponse command,
        string deploymentRoot,
        CancellationToken cancellationToken);

    Task PublishPreservationAsync(
        WorkerCommandClaimResponse command,
        string deploymentRoot,
        CancellationToken cancellationToken);
}