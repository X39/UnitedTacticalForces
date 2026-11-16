namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>Reports one attributed workload process independently of worker liveness.</summary>
public sealed record WorkerWorkloadObservation
{
    /// <summary>Gets stable service identifier of the observed workload.</summary>
    /// <value>Stable service identifier of the observed workload.</value>
    public string ServiceIdentifier { get; init; } = string.Empty;

    /// <summary>Gets ordinal distinguishing instances of the workload.</summary>
    /// <value>Ordinal distinguishing instances of the workload.</value>
    public int ServiceOrdinal { get; init; }

    /// <summary>Gets operating-system process identifier, when verified.</summary>
    /// <value>Operating-system process identifier, when verified.</value>
    public int? ProcessId { get; init; }

    /// <summary>Gets verified process start instant, when a process is attributed.</summary>
    /// <value>Verified process start instant, when a process is attributed.</value>
    public DateTimeOffset? ProcessStartedAt { get; init; }

    /// <summary>Gets observed execution state; Running does not imply game readiness.</summary>
    /// <value>Observed execution state; Running does not imply game readiness.</value>
    public EWorkerWorkloadState State { get; init; } = EWorkerWorkloadState.Unknown;

    /// <summary>Gets the UTC instant at which the worker observed this workload.</summary>
    /// <value>UTC instant at which the worker observed this workload.</value>
    public DateTimeOffset ObservedAt { get; init; }
}
