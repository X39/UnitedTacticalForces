namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Builds the update-stream paths used by the live fleet signal.
/// </summary>
/// <remarks>
/// <para>
/// A fleet socket is registered on, and the fleet change notifier publishes to, only the per-user path returned by
/// <see cref="ForUser"/>. Clients still request <c>game-servers/fleet</c> through the update stream controller,
/// which maps that request to the authenticated user's path.
/// </para>
/// <para>
/// No shared publish target exists on purpose: the update stream matches subscribers by a raw string prefix, so a
/// send to the bare <c>game-servers/fleet</c> prefix would reach every user's socket. The prefix is therefore a
/// private constant of this type, and no code outside it can address the shared prefix.
/// </para>
/// </remarks>
internal static class FleetChangeStreamPaths
{
    /// <summary>
    /// The update-stream path clients request when they subscribe to the live fleet signal.
    /// </summary>
    /// <remarks>
    /// Never registered and never published to; it is only the leading part of every string returned by
    /// <see cref="ForUser"/>.
    /// </remarks>
    /// <value>The string <c>game-servers/fleet</c>.</value>
    private const string FleetRequestPath = Constants.Routes.GameServers + "/fleet";

    /// <summary>
    /// Builds the update-stream path addressed to one user, the only string a fleet socket may be registered on and
    /// the only string the fleet change notifier publishes to.
    /// </summary>
    /// <remarks>
    /// The result has a fixed length of 55 characters (the 19 characters of <c>game-servers/fleet/</c> plus the 36
    /// characters of a <c>D</c>-format identifier), so no per-user path is a prefix of another user's path and the
    /// update stream's raw prefix matching can never deliver one user's signal to another user's socket.
    /// </remarks>
    /// <param name="userId">The identifier of the user the socket belongs to or the frame is addressed to.</param>
    /// <returns>
    /// The string <c>game-servers/fleet/{userId}</c> with the identifier formatted as lower-case hexadecimal digits
    /// separated by hyphens.
    /// </returns>
    public static string ForUser(Guid userId)
        => FleetRequestPath + "/" + userId.ToString("D");
}
