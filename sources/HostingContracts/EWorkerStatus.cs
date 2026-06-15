namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Last known state of a deployment's dedicated worker.
/// </summary>
public enum EWorkerStatus
{
    Provisioning = 0,
    Online = 1,
    Degraded = 2,
    Stopping = 3,
    Offline = 4,
    Faulted = 5,
}
