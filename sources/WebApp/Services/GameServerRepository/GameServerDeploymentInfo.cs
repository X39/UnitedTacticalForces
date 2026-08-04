namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Current deployment allocation, worker, billing-owner, and possible-cost facts.
/// </summary>
public sealed record GameServerDeploymentInfo
{
    public Guid PrimaryKey { get; init; }
    public string RuntimeProfileIdentifier { get; init; } = string.Empty;
    public string RuntimeProfileDisplayName { get; init; } = string.Empty;
    public decimal? EstimatedHourlyCostEur { get; init; }
    public string RuntimeStorageStrategy { get; init; } = "IncludedDisk";
    public long? PeakStorageRequiredBytes { get; init; }
    public long? IncludedStorageBytes { get; init; }
    public long? SelectedExternalStorageBytes { get; init; }
    public string DeploymentRoot { get; init; } = string.Empty;
    public decimal? EstimatedComputeHourlyCostEur { get; init; }
    public decimal? EstimatedStorageHourlyCostEur { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public bool PossibleCost { get; init; }
    public bool ProvisioningBlocked { get; init; }
    public Guid CostOwnerId { get; init; }
    public string CostOwnerName { get; init; } = string.Empty;
    public Guid? WorkerId { get; init; }
    public string? WorkerStatus { get; init; }
    public DateTimeOffset? LastHeartbeat { get; init; }
    public long? DiskTotalBytes { get; init; }
    public long? DiskAvailableBytes { get; init; }
    public DateTimeOffset? DiskTimeStampObserved { get; init; }
    public string? PublicAddress { get; init; }
    public string? ConnectionEndpoint { get; init; }
    public string? ConnectionProtocol { get; init; }
    public int? ConnectionPort { get; init; }
    public string? AdministrativeSshCommand { get; init; }
    public IReadOnlyCollection<GameServerDeploymentResourceInfo> Resources { get; init; } = [];
    public DateTimeOffset TimeStampCreated { get; init; }
    public DateTimeOffset? TimeStampStarted { get; init; }
    public DateTimeOffset? TimeStampResolved { get; init; }
    public string? FaultSummary { get; init; }
}
