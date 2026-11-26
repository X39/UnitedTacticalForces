namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Supplies, to singleton publishers such as the fleet change notifier, the users who may view game servers.
/// </summary>
/// <remarks>
/// The audience follows the per-server view rule of <see cref="IHostingAuthorizationService.HasServerPermission"/>
/// and is resolved from current data on every call.
/// </remarks>
internal interface IFleetAudienceSource
{
    /// <summary>
    /// Returns the users who may view each of the given game servers at the time of the call.
    /// </summary>
    /// <param name="gameServerIds">The ids of the game servers whose viewers are resolved.</param>
    /// <param name="cancellationToken">A token that cancels the resolution.</param>
    /// <returns>
    /// A task resolving to a dictionary with an entry for every requested server id, each listing the ids of the
    /// users who may view that server; an empty collection means nobody may view it.
    /// </returns>
    /// <remarks>
    /// A failure surfaces as an exception. The caller must treat a failure as "deliver to nobody" and never fall back
    /// to a broadcast.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetViewersAsync(
        IReadOnlyCollection<Guid> gameServerIds,
        CancellationToken cancellationToken);
}
