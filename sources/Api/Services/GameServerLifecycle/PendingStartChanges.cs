using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Single owner of the mark and consume rules of the pending-start signal: which kinds of applied change still wait
/// for the next start of a game server.
/// </summary>
/// <remarks>
/// <para>
/// The mod pack is frozen into the deployment content snapshot when a deployment is created at Start, while
/// configuration and credentials are re-read whenever a Start or Restart command is created. Each area is therefore
/// consumed at its own point and the signal stays truthful after a restart that applied only some areas.
/// </para>
/// <para>Known limitations accepted for this signal:</para>
/// <list type="bullet">
/// <item><description>
/// A configuration or credential change committed after a command resolved the configuration but before the
/// executor saved the consumption is cleared without having been applied. The change endpoints take no server lock,
/// so this narrow race is accepted.
/// </description></item>
/// <item><description>
/// A server that follows a profile whose latest revision changes is not flagged, because no server-side write
/// happens when the profile gains a revision.
/// </description></item>
/// <item><description>
/// Existing rows start at <see cref="EGameServerChangeArea.None"/>; there is no backfill.
/// </description></item>
/// </list>
/// </remarks>
internal static class PendingStartChanges
{
    /// <summary>
    /// Records that an applied change of the given area waits for the next start.
    /// </summary>
    /// <param name="server">The tracked game server that received the change.</param>
    /// <param name="area">The changed area. <see cref="EGameServerChangeArea.None"/> changes nothing.</param>
    /// <param name="now">The instant the change was applied. It becomes <see cref="GameServer.PendingStartSince"/> only when no change was pending before.</param>
    public static void MarkChanged(GameServer server, EGameServerChangeArea area, Instant now)
    {
        if (area == EGameServerChangeArea.None)
            return;
        server.PendingStartChangeAreas |= area;
        server.PendingStartSince ??= now;
    }

    /// <summary>
    /// Consumes the mod pack area because the deployment content snapshot was frozen.
    /// </summary>
    /// <param name="server">The tracked game server for which a deployment was created.</param>
    /// <returns><see langword="true"/> when the pending areas changed; otherwise <see langword="false"/>.</returns>
    public static bool ConsumeAtDeploymentCreation(GameServer server)
        => Clear(server, EGameServerChangeArea.ModPack);

    /// <summary>
    /// Consumes the configuration and credentials areas because a primary Start or Restart command rendered them.
    /// </summary>
    /// <param name="server">The tracked game server for which a command was created.</param>
    /// <param name="kind">The kind of the lifecycle job the command belongs to.</param>
    /// <param name="actionIdentifier">The action identifier the command was created for.</param>
    /// <param name="jobActionIdentifier">The action identifier of the owning lifecycle job.</param>
    /// <returns><see langword="true"/> when the pending areas changed; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Auxiliary and readiness commands of the same job carry another action identifier and must not consume a
    /// change that the primary command did not render.
    /// </remarks>
    public static bool ConsumeAtCommandCreation(
        GameServer server,
        ELifecycleJobKind kind,
        string actionIdentifier,
        string jobActionIdentifier)
    {
        if (kind is not (ELifecycleJobKind.Start or ELifecycleJobKind.Restart))
            return false;
        if (!string.Equals(actionIdentifier, jobActionIdentifier, StringComparison.Ordinal))
            return false;
        return Clear(server, EGameServerChangeArea.Configuration | EGameServerChangeArea.Credentials);
    }

    /// <summary>
    /// Expands a flag set into its single areas.
    /// </summary>
    /// <param name="flags">The flag set to expand.</param>
    /// <returns>The set areas in the order mod pack, configuration, credentials; empty when none is set.</returns>
    public static IReadOnlyList<EGameServerChangeArea> Areas(EGameServerChangeArea flags)
    {
        EGameServerChangeArea[] ordered =
        [
            EGameServerChangeArea.ModPack,
            EGameServerChangeArea.Configuration,
            EGameServerChangeArea.Credentials,
        ];
        return ordered.Where(q => (flags & q) != EGameServerChangeArea.None).ToArray();
    }

    /// <summary>
    /// Removes the given areas and resets the since-timestamp when no area remains.
    /// </summary>
    /// <param name="server">The tracked game server to update.</param>
    /// <param name="areas">The areas to remove.</param>
    /// <returns><see langword="true"/> when the pending areas changed; otherwise <see langword="false"/>.</returns>
    private static bool Clear(GameServer server, EGameServerChangeArea areas)
    {
        var remaining = server.PendingStartChangeAreas & ~areas;
        if (remaining == server.PendingStartChangeAreas)
            return false;
        server.PendingStartChangeAreas = remaining;
        if (remaining == EGameServerChangeArea.None)
            server.PendingStartSince = null;
        return true;
    }
}
