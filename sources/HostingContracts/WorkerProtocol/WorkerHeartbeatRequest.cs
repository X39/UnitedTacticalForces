using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Deployment-bound worker liveness and optional process evidence.
/// </summary>
public sealed record WorkerHeartbeatRequest
{
    public Guid DeploymentId { get; init; }

    [MaxLength(128)]
    public string? Stage { get; init; }

    public int? ProcessId { get; init; }

    public DateTimeOffset? ProcessStartedAt { get; init; }
}
