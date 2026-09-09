namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

public interface ISteamWorkshopMetadataClient
{
    Task<IReadOnlyCollection<SteamWorkshopMetadata>> GetDetailsAsync(
        IReadOnlyCollection<string> itemIdentifiers,
        CancellationToken cancellationToken = default);
}
