namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Persistent delivery and execution state of a worker command.
/// </summary>
public enum EWorkerCommandStatus
{
    Queued = 0,
    Received = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Retried = 5,
    Skipped = 6,
    CancelledByCostStop = 7,
}
