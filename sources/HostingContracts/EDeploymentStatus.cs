namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Persistent control-plane state of one deployment lifetime.
/// </summary>
public enum EDeploymentStatus
{
    Provisioning = 0,
    WaitingForWorker = 1,
    Preparing = 2,
    Running = 3,
    Stopping = 4,
    PreservationFailed = 5,
    Terminating = 6,
    Stopped = 7,
    TerminatingDueToFault = 8,
    TerminatedDueToFault = 9,
    Failed = 10,
    Terminated = 11,
}
