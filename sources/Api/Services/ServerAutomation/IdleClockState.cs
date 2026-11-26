namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The persisted idle clock of one game server.
/// </summary>
/// <param name="IdleDeploymentId">The deployment the idle clock belongs to, or <see langword="null" /> when it is not
/// running.</param>
/// <param name="IdleSince">The instant the server was first known to be empty, or <see langword="null" /> when the
/// clock is not running.</param>
/// <param name="IdleHeldSince">The instant the player count became unavailable while the clock was running, or
/// <see langword="null" /> when the clock is not held.</param>
public sealed record IdleClockState(Guid? IdleDeploymentId, Instant? IdleSince, Instant? IdleHeldSince)
{
    /// <summary>
    /// Gets the state of a clock that is not running.
    /// </summary>
    /// <value>A state without deployment, idle start, or hold.</value>
    public static IdleClockState Empty { get; } = new(null, null, null);
}
