using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

internal sealed class TestStorageDemandService : IStorageDemandService
{
    public Task<StorageDemandResult> EstimateAsync(
        string gameIdentifier,
        long? modPackDefinitionId,
        Guid? gameServerId,
        bool refreshMetadata,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new StorageDemandResult
        {
            Status = EStorageEstimateStatus.Incomplete,
            CalculatedAt = NodaSystemClock.Instance.GetCurrentInstant(),
        });
}
