using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The state of the idle shutdown clock of a game server.
/// </summary>
public sealed record ServerIdleStatusDto
{
    /// <summary>
    /// Gets the idle state.
    /// </summary>
    /// <value>Not running, players online, idle, or unknown while the player count is unavailable.</value>
    public required EAutomationIdleState State { get; init; }

    /// <summary>
    /// Gets the instant the server was first known to be empty.
    /// </summary>
    /// <value>The idle start, or <see langword="null"/> while players are online or the clock is not running.</value>
    public Instant? Since { get; init; }

    /// <summary>
    /// Gets the instant the idle stop happens.
    /// </summary>
    /// <value>One warning lead after the idle limit, or <see langword="null"/> unless the server is idle.</value>
    public Instant? StopsAt { get; init; }
}
