using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public interface ICloudWorkerStorageTransfer
{
    /// <summary>Downloads a reusable immutable artifact bound to a claimed command.</summary>
    /// <param name="command">Claimed command carrying the artifact declaration.</param>
    /// <param name="artifactIdentifier">Immutable artifact identifier.</param>
    /// <param name="destinationPath">Worker-owned destination path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the transfer.</returns>
    Task DownloadContentArtifactAsync(
        WorkerCommandClaimResponse command,
        string artifactIdentifier,
        string destinationPath,
        CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Worker content artifact transfer is not configured."));

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
