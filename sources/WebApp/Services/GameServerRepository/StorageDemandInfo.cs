namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record StorageDemandInfo
{
    public long SteadyStateRequiredBytes { get; init; }
    public long PreparationWorkspaceBytes { get; init; }
    public decimal SafetyMarginPercent { get; init; }
    public long SafetyMarginBytes { get; init; }
    public long PeakPreparationRequiredBytes { get; init; }
    /// <summary>Gets the server-calculated minimum raw external allocation, including filesystem headroom.</summary>
    /// <value>The minimum allocation in GiB, or null when external storage cannot satisfy demand.</value>
    public long? MinimumExternalCapacityGiB { get; init; }

    /// <summary>Gets the estimated writable bytes for the server's recommended external allocation.</summary>
    /// <value>Estimated usable bytes, or null when no external recommendation is available.</value>
    public long? RecommendedExternalUsableBytes { get; init; }

    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; init; }
    public DateTimeOffset? MeasurementTimestamp { get; init; }
    public IReadOnlyCollection<string> MissingComponents { get; init; } = [];
    public IReadOnlyCollection<StorageDemandComponentInfo> Components { get; init; } = [];
    public IReadOnlyCollection<RuntimeProfileStorageFitInfo> RuntimeProfiles { get; init; } = [];
}
