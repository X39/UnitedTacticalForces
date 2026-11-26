namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies what caused a lifecycle job to be queued.
/// </summary>
public enum ELifecycleJobTrigger
{
    /// <summary>
    /// A user requested the operation.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// The idle shutdown limit was reached.
    /// </summary>
    Idle = 1,

    /// <summary>
    /// The maximum runtime limit was reached.
    /// </summary>
    MaxRuntime = 2,

    /// <summary>
    /// A weekly schedule rule came due.
    /// </summary>
    Schedule = 3,
}
