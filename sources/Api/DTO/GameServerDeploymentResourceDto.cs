using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Durable and last-observed facts for one provider resource in a deployment graph.
/// </summary>
public sealed record GameServerDeploymentResourceDto
{
    public Guid PrimaryKey { get; init; }
    public string ProviderIdentifier { get; init; } = string.Empty;
    public string? ProviderResourceIdentifier { get; init; }
    public string Kind { get; init; } = string.Empty;
    public EDeploymentResourceState State { get; init; }
    public bool PossibleCost { get; init; }
    public string? ProviderState { get; init; }
    public string? PublicAddress { get; init; }
    public Instant? TimeStampObserved { get; init; }
    public Instant? TimeStampReleased { get; init; }
}
