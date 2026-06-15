namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies the durable lifecycle operation represented by a job.
/// </summary>
public enum ELifecycleJobKind
{
    Start = 0,
    Stop = 1,
    Terminate = 2,
    Restart = 3,
    Install = 4,
    Update = 5,
    CollectLogs = 6,
    Custom = 7,
}
