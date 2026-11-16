using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Reports a running or successful command with optional process evidence.
/// </summary>
public sealed record WorkerCommandProcessRequest
{
    /// <summary>Gets workload evidence for this protocol message.</summary>
    /// <value>Observed workloads at this command transition.</value>
    public IReadOnlyCollection<WorkerWorkloadObservation> Workloads { get; init; } = [];

    public Guid DeploymentId { get; init; }

    [Required, MaxLength(128)]
    public string Stage { get; init; } = string.Empty;

    public int? ProcessId { get; init; }

    public DateTimeOffset? ProcessStartedAt { get; init; }

    [MaxLength(16384)]
    public string? Output { get; init; }

    [MaxLength(512)]
    public IReadOnlyCollection<WorkerStorageMeasurement> StorageMeasurements { get; init; } = [];

    /// <summary>Gets concrete versions observed while resolving provider-floating content.</summary>
    [MaxLength(512)]
    public IReadOnlyCollection<WorkerObservedContentVersion> ObservedContentVersions { get; init; } = [];
}
