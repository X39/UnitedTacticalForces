using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public interface IWorkerCommandExecutor
{
    Task<WorkerCommandExecutionResult> ExecuteAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken);

    Task<WorkerCommandExecutionResult?> ReconcileRedeliveryAsync(
        WorkerCommandClaimResponse command,
        WorkerCommandJournalEntry journalEntry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes deployment-local runtime side effects after the control plane rejects a command transition.
    /// The default keeps non-runtime executors source compatible.
    /// </summary>
    Task CompensateSupersededAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken);
}