namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies the action a weekly schedule rule performs when it comes due.
/// </summary>
public enum EServerScheduleAction
{
    /// <summary>
    /// Starts the server at the scheduled time.
    /// </summary>
    Start = 0,

    /// <summary>
    /// Stops the server at the scheduled time.
    /// </summary>
    Stop = 1,
}
