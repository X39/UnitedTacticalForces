namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerRuntimeStatus(
    string State,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAt,
    string? Detail,
    long? DeploymentStorageTotalBytes = null,
    long? DeploymentStorageAvailableBytes = null,
    long? ControlledDeploymentBytes = null)
{
    /// <summary>Gets independently observed processes belonging to this worker's assigned workloads.</summary>
    /// <value>Current workload observations; empty for executors using the legacy single-process protocol.</value>
    public IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerWorkloadObservation>
        Workloads { get; init; } = [];
}
