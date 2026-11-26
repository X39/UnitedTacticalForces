namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies the kinds of applied game-server change that still wait for the next start.
/// </summary>
/// <remarks>
/// The values are persisted as an integer bit set. Existing members must never be renumbered or reused.
/// </remarks>
[Flags]
public enum EGameServerChangeArea
{
    /// <summary>
    /// No applied change waits for the next start.
    /// </summary>
    None = 0,

    /// <summary>
    /// The mod pack selection changed. The content snapshot is frozen when the deployment is created at Start, so
    /// this area is consumed at deployment creation.
    /// </summary>
    ModPack = 1,

    /// <summary>
    /// Configuration values, resets, the selected profile or the revision mode changed. Configuration is resolved
    /// when a Start or Restart command is created, so this area is consumed at that command creation.
    /// </summary>
    Configuration = 2,

    /// <summary>
    /// A server credential changed. Credentials are resolved when a Start or Restart command is created, so this
    /// area is consumed at that command creation.
    /// </summary>
    Credentials = 4,
}
