namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Accepts notifications that a game server's lifecycle state changed and publishes them to fleet subscribers.
/// </summary>
/// <remarks>
/// <para>
/// Placement: the fleet signal is explicitly requested by the phase 62 decisions D-13 to D-17. It observes
/// lifecycle state and publishes a wire message, so it belongs beside the lifecycle services in
/// <c>Services/GameServerLifecycle</c>. It is not placed in the update stream service (that would widen shared
/// infrastructure and change event streams), not in the WebSocket contracts project (wire messages only), and not
/// in a controller.
/// </para>
/// <para>
/// What the signal touches: the fleet branch of the update stream controller, which registers each socket on its
/// user's path; the path builder <see cref="FleetChangeStreamPaths"/>; the server view audience query of
/// <see cref="IHostingAuthorizationService"/>; the singleton audience bridge <see cref="IFleetAudienceSource"/>;
/// and the notifier itself, which delivers each change only to the users who may view the changed server. The
/// update stream service and the WebSocket contracts are consumed unchanged. The signal contains no game-process,
/// provider or worker logic.
/// </para>
/// </remarks>
internal interface IFleetChangeNotifier
{
    /// <summary>
    /// Queues a fleet-changed signal for the specified game server.
    /// </summary>
    /// <remarks>
    /// The call is non-blocking and thread-safe and never throws. Delivery happens asynchronously and may coalesce
    /// several notifications for the same server into one message.
    /// </remarks>
    /// <param name="gameServerId">The primary key of the game server whose lifecycle state changed.</param>
    void Notify(Guid gameServerId);
}
