namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Persistent execution state of a lifecycle job.
/// </summary>
public enum ELifecycleJobStatus
{
    Queued = 0,
    Running = 1,
    RetryPending = 2,
    WaitingForWorker = 3,
    Succeeded = 4,
    Failed = 5,
    Skipped = 6,
    CancelledByCostStop = 7,
}
