namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies why the control plane opened an automatic stop warning for a running deployment.
/// </summary>
public enum EAutoStopReason
{
    /// <summary>
    /// The server stayed without connected players for the configured idle duration.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// The session reached the configured maximum runtime.
    /// </summary>
    MaxRuntime = 1,

    /// <summary>
    /// A weekly schedule rule with the stop action came due.
    /// </summary>
    Schedule = 2,
}
