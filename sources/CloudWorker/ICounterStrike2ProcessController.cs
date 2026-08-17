namespace X39.UnitedTacticalForces.CloudWorker;

public interface ICounterStrike2ProcessController
{
    Task<CounterStrike2ProcessObservation> ObserveAsync(CancellationToken cancellationToken);
    Task<WorkerCommandExecutionResult> StartAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    Task<WorkerCommandExecutionResult> StopAsync(bool force, TimeSpan timeout, int attempts, CancellationToken cancellationToken);
    Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken);
}
