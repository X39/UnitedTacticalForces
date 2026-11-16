using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

public sealed record StorageDemandDto
{
    public long SteadyStateRequiredBytes { get; init; }
    public long PreparationWorkspaceBytes { get; init; }
    public decimal SafetyMarginPercent { get; init; }
    public long SafetyMarginBytes { get; init; }
    public long PeakPreparationRequiredBytes { get; init; }
    /// <summary>Gets the smallest provider-supported raw external allocation that covers peak demand after filesystem overhead.</summary>
    /// <value>The allocation in GiB, or null when external storage cannot satisfy the demand.</value>
    public long? MinimumExternalCapacityGiB { get; init; }

    /// <summary>Gets the estimated writable capacity of the recommended external allocation.</summary>
    /// <value>Usable bytes after filesystem headroom, or null when no allocation is available.</value>
    public long? RecommendedExternalUsableBytes { get; init; }

    /// <summary>Gets the aggregate next-deployment estimate for saved workload placement.</summary>
    /// <value>A current estimate, or null for creation-only requests or unavailable configuration and pricing.</value>
    public WorkloadPlacementEstimateDto? PlacementEstimate { get; init; }

    public EStorageEstimateStatus Status { get; init; }
    public Instant CalculatedAt { get; init; }
    public Instant? MeasurementTimestamp { get; init; }
    public IReadOnlyCollection<string> MissingComponents { get; init; } = [];
    public IReadOnlyCollection<StorageDemandComponentDto> Components { get; init; } = [];
    public IReadOnlyCollection<RuntimeProfileStorageFitDto> RuntimeProfiles { get; init; } = [];
}
