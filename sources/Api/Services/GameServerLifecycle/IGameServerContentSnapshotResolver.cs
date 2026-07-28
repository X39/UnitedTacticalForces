using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IGameServerContentSnapshotResolver
{
    Task<IReadOnlyCollection<WorkerContentReference>> ResolveAsync(
        Guid gameServerId,
        CancellationToken cancellationToken = default);
}
