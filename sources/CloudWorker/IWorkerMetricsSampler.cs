namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Samples the CPU and memory of the machine that hosts the game server.</summary>
public interface IWorkerMetricsSampler
{
    /// <summary>Takes one machine reading.</summary>
    /// <returns>A reading whose figures are measured values or explicit unavailable reasons, never invented zeros.</returns>
    WorkerMachineReading Sample();
}
