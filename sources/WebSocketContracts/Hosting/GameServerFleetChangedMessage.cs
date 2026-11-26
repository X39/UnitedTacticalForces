namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

/// <summary>
/// Signals over the update stream that one game server of the fleet changed.
/// </summary>
/// <remarks>
/// The message deliberately carries no server data besides the identifier. Subscribers must refetch the fleet
/// through the authorized REST endpoint, which applies the per-server visibility rules; this message never
/// discloses titles, states, costs or owners. Cost values travel only in <see cref="GameServerAccruedCostMessage"/>,
/// <see cref="CostAlertRaisedMessage"/> and <see cref="CostAlertDismissedMessage"/>, which are addressed only to
/// users who may view the cost owner.
/// </remarks>
public sealed class GameServerFleetChangedMessage
{
    /// <summary>
    /// Gets the primary key of the game server whose lifecycle state changed.
    /// </summary>
    /// <value>
    /// The identifier of the changed game server. Clients use it only as a refetch trigger and must load the
    /// server state through the authorized endpoint.
    /// </value>
    public required Guid ServerId { get; init; }
}
