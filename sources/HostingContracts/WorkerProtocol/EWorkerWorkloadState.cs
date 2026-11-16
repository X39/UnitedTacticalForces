namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>Describes observed process execution without asserting game admission or readiness.</summary>
public enum EWorkerWorkloadState
{
    /// <summary>The workload has no running process.</summary>
    Stopped,
    /// <summary>The workload is being started.</summary>
    Starting,
    /// <summary>The attributed process is running; game readiness is not implied.</summary>
    Running,
    /// <summary>The workload encountered an execution failure.</summary>
    Failed,
    /// <summary>The worker cannot establish the workload's process state.</summary>
    Unknown,
}
