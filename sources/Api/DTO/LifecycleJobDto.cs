using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Durable lifecycle operation projection.
/// </summary>
public sealed record LifecycleJobDto
{
    public Guid PrimaryKey { get; init; }
    public Guid GameServerId { get; init; }
    public Guid? DeploymentId { get; init; }
    public ELifecycleJobKind Kind { get; init; }
    public string ActionId { get; init; } = string.Empty;
    public ELifecycleJobStatus Status { get; init; }
    public string Stage { get; init; } = string.Empty;
    public int ProgressPercent { get; init; }
    public int AttemptCount { get; init; }
    public string? FailureReason { get; init; }
    public bool CanRetry { get; init; }
    public bool CanSkip { get; init; }
    public Instant TimeStampCreated { get; init; }
    public Instant TimeStampUpdated { get; init; }
    public Instant? TimeStampStarted { get; init; }
    public Instant? TimeStampCompleted { get; init; }
    public IReadOnlyCollection<WorkerCommandDto> Commands { get; init; } = [];
}
