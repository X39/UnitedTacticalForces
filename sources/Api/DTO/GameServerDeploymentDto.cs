using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Current deployment allocation, worker, and possible-cost facts.
/// </summary>
public sealed record GameServerDeploymentDto
{
    public Guid PrimaryKey { get; init; }
    public EDeploymentStatus Status { get; init; }
    public string Stage { get; init; } = string.Empty;
    public bool PossibleCost { get; init; }
    public bool ProvisioningBlocked { get; init; }
    public Guid CostOwnerId { get; init; }
    public string CostOwnerName { get; init; } = string.Empty;
    public Guid? WorkerId { get; init; }
    public EWorkerStatus? WorkerStatus { get; init; }
    public Instant? LastHeartbeat { get; init; }
    public string? PublicAddress { get; init; }
    public string? ConnectionEndpoint { get; init; }
    public string? ConnectionProtocol { get; init; }
    public int? ConnectionPort { get; init; }
    public IReadOnlyCollection<GameServerDeploymentResourceDto> Resources { get; init; } = [];
    public Instant TimeStampCreated { get; init; }
    public Instant? TimeStampStarted { get; init; }
    public Instant? TimeStampResolved { get; init; }
    public string? FaultSummary { get; init; }
}
