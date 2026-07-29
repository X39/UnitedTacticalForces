using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

public sealed record StorageDemandResult
{
    public long SteadyStateRequiredBytes { get; init; }
    public long PreparationWorkspaceBytes { get; init; }
    public decimal SafetyMarginPercent { get; init; }
    public long SafetyMarginBytes { get; init; }
    public long PeakPreparationRequiredBytes { get; init; }
    public EStorageEstimateStatus Status { get; init; }
    public Instant CalculatedAt { get; init; }
    public Instant? MeasurementTimestamp { get; init; }
    public IReadOnlyCollection<string> MissingComponents { get; init; } = [];
    public IReadOnlyCollection<StorageDemandComponent> Components { get; init; } = [];
    public IReadOnlyCollection<Hosting.Contracts.WorkerProtocol.WorkerContentReference> EffectiveContent { get; init; }
        = [];
    public long ProfileSystemReserveBytes { get; init; }
}
