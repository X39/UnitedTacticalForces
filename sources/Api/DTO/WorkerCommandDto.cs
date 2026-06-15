using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Deployment-targeted worker command projection.
/// </summary>
public sealed record WorkerCommandDto
{
    public Guid PrimaryKey { get; init; }
    public long Sequence { get; init; }
    public string ActionId { get; init; } = string.Empty;
    public EWorkerCommandStatus Status { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
    public Instant TimeStampCreated { get; init; }
    public Instant? TimeStampStarted { get; init; }
    public Instant? TimeStampCompleted { get; init; }
}
