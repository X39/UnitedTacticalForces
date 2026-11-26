namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies what a cost alert record announces.
/// </summary>
public enum ECostAlertKind
{
    /// <summary>
    /// The accrued cost of a session reached a cost owner threshold.
    /// </summary>
    CostThreshold = 0,

    /// <summary>
    /// An automatic stop warning was opened for a session.
    /// </summary>
    AutoStop = 1,
}
