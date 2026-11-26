namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Describes whether idle shutdown is available for the game of a server.
/// </summary>
public sealed record ServerIdleShutdownCapabilityDto
{
    /// <summary>
    /// Gets whether idle shutdown can be used.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the game reports its player count; <see langword="false"/> when idle shutdown
    /// cannot work because the game cannot tell whether players are connected.
    /// </value>
    public required bool Available { get; init; }
}
