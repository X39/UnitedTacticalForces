using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Deployment-bound worker liveness and optional process evidence.
/// </summary>
public sealed record WorkerHeartbeatRequest : IValidatableObject
{
    /// <summary>Gets workload evidence for this protocol message.</summary>
    /// <value>Complete observations of workloads assigned to this worker host.</value>
    public IReadOnlyCollection<WorkerWorkloadObservation> Workloads { get; init; } = [];

    public Guid DeploymentId { get; init; }

    [MaxLength(128)]
    public string? Stage { get; init; }

    public int? ProcessId { get; init; }

    public DateTimeOffset? ProcessStartedAt { get; init; }

    [Range(0, long.MaxValue)]
    public long? DeploymentStorageTotalBytes { get; init; }

    [Range(0, long.MaxValue)]
    public long? DeploymentStorageAvailableBytes { get; init; }

    [Range(0, long.MaxValue)]
    public long? ControlledDeploymentBytes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeploymentStorageTotalBytes.HasValue != DeploymentStorageAvailableBytes.HasValue)
        {
            yield return new ValidationResult(
                "Deployment storage telemetry must provide both total and available bytes or neither.",
                [nameof(DeploymentStorageTotalBytes), nameof(DeploymentStorageAvailableBytes)]);
        }
        if (DeploymentStorageAvailableBytes > DeploymentStorageTotalBytes)
        {
            yield return new ValidationResult(
                "Deployment storage available bytes cannot exceed total bytes.",
                [nameof(DeploymentStorageAvailableBytes)]);
        }
        if (ControlledDeploymentBytes.HasValue != DeploymentStorageTotalBytes.HasValue)
        {
            yield return new ValidationResult(
                "Controlled deployment bytes must accompany deployment storage telemetry.",
                [nameof(ControlledDeploymentBytes)]);
        }
    }
}
