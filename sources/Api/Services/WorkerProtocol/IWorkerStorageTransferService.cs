using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

public interface IWorkerStorageTransferService
{
    /// <summary>Opens a reusable artifact declared by the authenticated command.</summary>
    /// <param name="workerId">Worker identifier.</param>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="artifactIdentifier">Artifact identifier.</param>
    /// <param name="workerCredential">Deployment-scoped worker credential.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Readable artifact.</returns>
    Task<ModArtifactDownload> OpenContentArtifactAsync(
        Guid workerId,
        Guid commandId,
        string artifactIdentifier,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerStorageArchiveDownload> CreatePersistentSnapshotAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerPreservationPlanResponse> GetPreservationPlanAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerPreservationPublicationResponse> PublishPreservationAsync(
        Guid workerId,
        Guid commandId,
        string workerCredential,
        Stream archive,
        CancellationToken cancellationToken = default);
}
