using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

internal sealed class UnavailableCloudWorkerStorageTransfer : ICloudWorkerStorageTransfer
{
    public Task ResetPersistentOverlayAsync(
        string deploymentRoot,
        CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Worker persistent storage transfer is not configured."));

    public Task ApplyPersistentSnapshotAsync(
        WorkerCommandClaimResponse command,
        string deploymentRoot,
        CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Worker persistent storage transfer is not configured."));

    public Task PublishPreservationAsync(
        WorkerCommandClaimResponse command,
        string deploymentRoot,
        CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Worker preservation transfer is not configured."));
}