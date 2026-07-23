namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Provider-neutral operational history for one game-server deployment or resource.
/// </summary>
public sealed record HostingOperationInfo
{
    public Guid PrimaryKey { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public string? ProviderIdentifier { get; init; }
    public string? ResourceKind { get; init; }
    public string? ResourceIdentifier { get; init; }
    public string? ProviderState { get; init; }
    public Guid? DeploymentId { get; init; }
    public Guid? LifecycleJobId { get; init; }
    public Guid? WorkerId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public DateTimeOffset TimeStampCreated { get; init; }
}
