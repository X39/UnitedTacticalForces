namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies the state of one automatic stop warning episode.
/// </summary>
public enum EAutoStopStatus
{
    /// <summary>
    /// The warning is open and the stop has not been requested yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The warning elapsed and the control plane queued an ordinary stop job.
    /// </summary>
    Executed = 1,

    /// <summary>
    /// The warning was cancelled before the stop, for example because players connected or the owner kept the
    /// session running.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// The warning was replaced because the deployment stopped or ended by other means.
    /// </summary>
    Superseded = 3,
}
