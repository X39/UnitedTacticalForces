namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record StorageDemandInfo
{
    public long SteadyStateRequiredBytes { get; init; }
    public long PreparationWorkspaceBytes { get; init; }
    public decimal SafetyMarginPercent { get; init; }
    public long SafetyMarginBytes { get; init; }
    public long PeakPreparationRequiredBytes { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; init; }
    public DateTimeOffset? MeasurementTimestamp { get; init; }
    public IReadOnlyCollection<string> MissingComponents { get; init; } = [];
    public IReadOnlyCollection<StorageDemandComponentInfo> Components { get; init; } = [];
    public IReadOnlyCollection<RuntimeProfileStorageFitInfo> RuntimeProfiles { get; init; } = [];
}
