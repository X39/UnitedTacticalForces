using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

public interface IWorkerStorageTransferService
{
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