namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Secret-safe worker outcome persisted locally before it is reported.
/// </summary>
public sealed record WorkerCommandExecutionResult(
    bool Succeeded,
    string Stage,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? Output = null,
    int? ProcessId = null,
    DateTimeOffset? ProcessStartedAt = null,
    IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerStorageMeasurement>?
        StorageMeasurements = null,
    IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerObservedContentVersion>?
        ObservedContentVersions = null)
{
    /// <summary>Gets the workload observations captured when this command completed.</summary>
    /// <value>Process evidence for each supervised workload, without implying application-level readiness.</value>
    public IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerWorkloadObservation>
        Workloads { get; init; } = [];

    public static WorkerCommandExecutionResult Success(
        string stage,
        string? output = null,
        int? processId = null,
        DateTimeOffset? processStartedAt = null)
        => new(true, stage, Output: output, ProcessId: processId, ProcessStartedAt: processStartedAt);

    public static WorkerCommandExecutionResult Failure(string stage, string code, string message)
        => new(false, stage, code, message);
}
