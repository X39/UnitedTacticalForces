namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Durable lifecycle work and server-projected intervention availability.
/// </summary>
public sealed record LifecycleJobInfo
{
    public Guid PrimaryKey { get; init; }
    public Guid GameServerId { get; init; }
    public Guid? DeploymentId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string ActionId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public int ProgressPercent { get; init; }
    public int AttemptCount { get; init; }
    public bool CanRetry { get; init; }
    public bool CanSkip { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset TimeStampCreated { get; init; }
    public DateTimeOffset TimeStampUpdated { get; init; }
    public DateTimeOffset? TimeStampStarted { get; init; }
    public DateTimeOffset? TimeStampCompleted { get; init; }
    public IReadOnlyCollection<WorkerCommandInfo> Commands { get; init; } = [];
}
