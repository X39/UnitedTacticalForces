namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Controls and observes the deployment-attributed ASKA Wine process group.</summary>
public interface IAskaProcessController
{
    /// <summary>Observes the currently attributed process.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verified process state.</returns>
    Task<AskaProcessObservation> ObserveAsync(CancellationToken cancellationToken);

    /// <summary>Starts ASKA and waits for bounded readiness.</summary>
    /// <param name="secrets">Secret values used only for output redaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    Task<WorkerCommandExecutionResult> StartAsync(
        IEnumerable<string> secrets,
        CancellationToken cancellationToken);

    /// <summary>Stops ASKA gracefully after readiness or forcefully before readiness.</summary>
    /// <param name="force">Whether to skip graceful signaling.</param>
    /// <param name="timeout">Bounded stop timeout.</param>
    /// <param name="attempts">Graceful signal attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    Task<WorkerCommandExecutionResult> StopAsync(
        bool force,
        TimeSpan timeout,
        int attempts,
        CancellationToken cancellationToken);

    /// <summary>Terminates the attributed process tree.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken);
}
