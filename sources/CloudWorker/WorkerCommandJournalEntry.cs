namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Durable local receipt/outcome keyed by the control plane's stable command GUID.
/// It intentionally stores no command payload or raw secret material.
/// </summary>
public sealed record WorkerCommandJournalEntry(
    Guid CommandId,
    Guid DeploymentId,
    Guid WorkerId,
    long Sequence,
    string ActionIdentifierHash,
    string CommandContentHash,
    EWorkerCommandJournalState State,
    string Stage,
    DateTimeOffset TimeStampReceived,
    DateTimeOffset TimeStampUpdated,
    int? ProcessIdBeforeExecution,
    DateTimeOffset? ProcessStartedAtBeforeExecution,
    WorkerCommandExecutionResult? Outcome)
{
    /// <summary>Gets the workload incarnations observed before the first execution attempt.</summary>
    /// <value>Original process evidence retained across redelivery and worker restarts.</value>
    public IReadOnlyCollection<X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerWorkloadObservation>
        WorkloadsBeforeExecution { get; init; } = [];
}
