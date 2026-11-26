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
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }

    /// <summary>Nickname of the user who requested the job, the hidden-member placeholder for a deleted user, or null when the platform requested it.</summary>
    /// <value>The requester's display name, the placeholder <c>Hidden member</c>, or <see langword="null"/> for a platform action.</value>
    public string? RequestedByName { get; init; }

    /// <summary>What caused the job. The UI shows a trigger line instead of the actor line for automatic jobs.</summary>
    /// <value><see cref="ELifecycleJobTrigger.Manual"/> for a user request, otherwise the automatic cause.</value>
    public ELifecycleJobTrigger Trigger { get; init; }

    /// <summary>The automation limit that applied when the job was queued automatically.</summary>
    /// <value>Minutes for <see cref="ELifecycleJobTrigger.Idle"/>, hours for <see cref="ELifecycleJobTrigger.MaxRuntime"/>, otherwise <see langword="null"/>.</value>
    public int? TriggerLimit { get; init; }
    public bool CanRetry { get; init; }
    public bool CanSkip { get; init; }
    public Instant TimeStampCreated { get; init; }
    public Instant TimeStampUpdated { get; init; }
    public Instant? TimeStampStarted { get; init; }
    public Instant? TimeStampCompleted { get; init; }
    public IReadOnlyCollection<WorkerCommandDto> Commands { get; init; } = [];
}
