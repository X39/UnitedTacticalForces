namespace X39.UnitedTacticalForces.WebApp.Services.MediaAssetRepository;

public record MediaAssetUsageInfo
{
    public string Domain { get; init; } = string.Empty;

    public string DomainKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}
