namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Identifies what the automation evaluator does with the pending automatic stop of one deployment.
/// </summary>
public enum EPendingStopDecision
{
    /// <summary>
    /// Nothing changes: no warning is open and none is due, or the open warning keeps waiting.
    /// </summary>
    None = 0,

    /// <summary>
    /// A new warning episode is opened and its owner is alerted; the stop follows one warning lead later.
    /// </summary>
    Warn = 1,

    /// <summary>
    /// The open warning is withdrawn because the condition that caused it no longer holds.
    /// </summary>
    Cancel = 2,

    /// <summary>
    /// The open warning is replaced because the deployment stopped running or another stop is already queued.
    /// </summary>
    Supersede = 3,

    /// <summary>
    /// The open warning elapsed and the ordinary stop job is queued.
    /// </summary>
    Execute = 4,

    /// <summary>
    /// A limit came due while the platform was not evaluating, so the stop is queued at once without a warning.
    /// </summary>
    ExecuteWithoutWarning = 5,

    /// <summary>
    /// The open warning is superseded because an earlier stop applies; the earlier candidate is warned about, or
    /// executed when it is already due.
    /// </summary>
    Replace = 6,
}
