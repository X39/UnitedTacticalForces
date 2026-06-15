namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Durable and last-observed facts for one cloud-provider deployment resource.
/// </summary>
public sealed record GameServerDeploymentResourceInfo
{
    public Guid PrimaryKey { get; init; }
    public string ProviderIdentifier { get; init; } = string.Empty;
    public string? ProviderResourceIdentifier { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public bool PossibleCost { get; init; }
    public string? ProviderState { get; init; }
    public string? PublicAddress { get; init; }
    public DateTimeOffset? TimeStampObserved { get; init; }
    public DateTimeOffset? TimeStampReleased { get; init; }
}
