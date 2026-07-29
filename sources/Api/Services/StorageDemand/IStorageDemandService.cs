namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

public interface IStorageDemandService
{
    Task<StorageDemandResult> EstimateAsync(
        string gameIdentifier,
        long? modPackDefinitionId,
        Guid? gameServerId,
        bool refreshMetadata,
        CancellationToken cancellationToken = default);
}
