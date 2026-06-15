namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Stable, deployment-bound command delivery envelope.
/// </summary>
public sealed record WorkerCommandClaimResponse
{
    public Guid CommandId { get; init; }

    public Guid LifecycleJobId { get; init; }

    public Guid DeploymentId { get; init; }

    public Guid WorkerId { get; init; }

    public long Sequence { get; init; }

    public string ActionIdentifier { get; init; } = string.Empty;

    /// <summary>
    /// Opaque JSON command payload. The worker validates action-specific content.
    /// </summary>
    public string Payload { get; init; } = "{}";

    public int Attempt { get; init; }

    public DateTimeOffset LeaseExpiresAt { get; init; }
}
