namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record StorageDemandEstimateRequestInfo
{
    public string GameIdentifier { get; init; } = string.Empty;
    public long? ModPackDefinitionId { get; init; }
    public string RuntimeProviderIdentifier { get; init; } = string.Empty;
}
