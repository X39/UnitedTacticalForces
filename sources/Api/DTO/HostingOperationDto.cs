using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One provider-neutral operational event for a game-server deployment or resource.
/// </summary>
public sealed record HostingOperationDto
{
    public Guid PrimaryKey { get; init; }
    public EHostingAuditEventKind Kind { get; init; }
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
    public Instant TimeStampCreated { get; init; }
}
