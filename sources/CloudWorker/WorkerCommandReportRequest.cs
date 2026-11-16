namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerCommandReportRequest(
    Guid DeploymentId,
    EWorkerCommandReportState State,
    string Stage,
    DateTimeOffset ObservedAt,
    string? ErrorCode,
    string? ErrorMessage,
    string? Output,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAt,
    IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerStorageMeasurement>?
        StorageMeasurements = null,
    IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerObservedContentVersion>?
        ObservedContentVersions = null)
{
    /// <summary>Gets workload evidence accompanying the command transition.</summary>
    /// <value>The observed process states forwarded through the existing worker reporting endpoint.</value>
    public IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerWorkloadObservation>
        Workloads { get; init; } = [];
}
