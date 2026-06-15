namespace X39.UnitedTacticalForces.CloudWorker;

public interface IArma3ProcessController
{
    Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken);

    Task<WorkerCommandExecutionResult> StartAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);

    Task<WorkerCommandExecutionResult> StopAsync(
        bool force,
        TimeSpan timeout,
        int gracefulAttempts,
        CancellationToken cancellationToken);

    Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
        => StopAsync(true, TimeSpan.Zero, 1, cancellationToken);
}