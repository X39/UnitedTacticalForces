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

    /// <summary>
    /// Observes the current player count of the running game server.
    /// </summary>
    /// <remarks>
    /// Executors without a reliable player source keep this default, so an unknown game reports
    /// game-does-not-report instead of a number. Only the worker measures players; the API never queries a game.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A measured count or exactly one unavailable reason.</returns>
    Task<WorkerPlayerObservation> ObservePlayersAsync(CancellationToken cancellationToken)
        => Task.FromResult(WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.GameDoesNotReport));

    Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken);
}