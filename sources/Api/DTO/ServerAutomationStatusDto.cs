namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The live automation status of a game server, calculated by the control plane.
/// </summary>
public sealed record ServerAutomationStatusDto
{
    /// <summary>
    /// Gets the state of the idle shutdown clock.
    /// </summary>
    /// <value>The idle state with its start and stop instants.</value>
    public required ServerIdleStatusDto Idle { get; init; }

    /// <summary>
    /// Gets the instant the maximum runtime stop happens.
    /// </summary>
    /// <value>
    /// One warning lead after the runtime limit, or <see langword="null"/> when the limit is off or the server is
    /// not running.
    /// </value>
    public Instant? MaxRuntimeStopsAt { get; init; }

    /// <summary>
    /// Gets the next scheduled action of the server.
    /// </summary>
    /// <value>The earliest upcoming rule occurrence, or <see langword="null"/> when none will run.</value>
    public ServerNextScheduledDto? NextScheduled { get; init; }

    /// <summary>
    /// Gets the automatic stop that is announced and has not happened yet.
    /// </summary>
    /// <value>The pending stop, or <see langword="null"/> when no stop is announced.</value>
    public ServerPendingStopDto? PendingStop { get; init; }
}
