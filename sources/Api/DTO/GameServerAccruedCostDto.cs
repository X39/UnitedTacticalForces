namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The accrued cost of the current or most recent session of one game server.
/// </summary>
public sealed record GameServerAccruedCostDto
{
    /// <summary>
    /// Gets the game server the accrued cost belongs to.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid GameServerId { get; init; }

    /// <summary>
    /// Gets the accrued cost of the session.
    /// </summary>
    /// <value>
    /// The accrued cost of the active session, or of the most recent session that cost once it ended, until a new
    /// session starts costing. <see langword="null"/> means the server never had a session that cost; an unavailable
    /// amount inside the session is <see cref="AccruedCostDto.Amount"/> null, never zero.
    /// </value>
    public AccruedCostDto? Session { get; init; }
}
