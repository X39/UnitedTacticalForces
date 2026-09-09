using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

internal sealed class UnavailableCloudWorkerStorageTransfer : ICloudWorkerStorageTransfer
{
    /// <inheritdoc />
    public Task DownloadContentArtifactAsync(
        WorkerCommandClaimResponse command,
        string artifactIdentifier,
        string destinationPath,
        CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Worker content artifact transfer is not configured."));

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
