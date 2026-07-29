namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record RuntimeProfileStorageFitInfo
{
    public string RuntimeProviderIdentifier { get; init; } = string.Empty;
    public string RuntimeProfileIdentifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public long? IncludedCapacityBytes { get; init; }
    public long? UsableCapacityBytes { get; init; }
    public bool? IsSufficient { get; init; }
    public bool ExternalStorageRequired { get; init; }
}
