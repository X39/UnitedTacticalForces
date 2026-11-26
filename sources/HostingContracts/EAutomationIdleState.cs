namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// The observable state of the idle shutdown clock of one game server.
/// </summary>
public enum EAutomationIdleState
{
    /// <summary>
    /// The idle clock is not running: idle shutdown is off, the game cannot report players, or no deployment is
    /// running.
    /// </summary>
    NotRunning = 0,

    /// <summary>
    /// Players are online or a lifecycle job is active, so the server is not idle.
    /// </summary>
    PlayersOnline = 1,

    /// <summary>
    /// The server is known to be empty and the idle clock is counting.
    /// </summary>
    Idle = 2,

    /// <summary>
    /// The player count is currently unavailable; the idle clock is held and neither advances nor resets.
    /// </summary>
    Unknown = 3,
}
