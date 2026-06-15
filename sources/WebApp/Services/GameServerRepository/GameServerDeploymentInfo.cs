namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Current deployment allocation, worker, billing-owner, and possible-cost facts.
/// </summary>
public sealed record GameServerDeploymentInfo
{
    public Guid PrimaryKey { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public bool PossibleCost { get; init; }
    public bool ProvisioningBlocked { get; init; }
    public Guid CostOwnerId { get; init; }
    public string CostOwnerName { get; init; } = string.Empty;
    public Guid? WorkerId { get; init; }
    public string? WorkerStatus { get; init; }
    public DateTimeOffset? LastHeartbeat { get; init; }
    public string? PublicAddress { get; init; }
    public string? ConnectionEndpoint { get; init; }
    public string? ConnectionProtocol { get; init; }
    public int? ConnectionPort { get; init; }
    public IReadOnlyCollection<GameServerDeploymentResourceInfo> Resources { get; init; } = [];
    public DateTimeOffset TimeStampCreated { get; init; }
    public DateTimeOffset? TimeStampStarted { get; init; }
    public DateTimeOffset? TimeStampResolved { get; init; }
    public string? FaultSummary { get; init; }
}
