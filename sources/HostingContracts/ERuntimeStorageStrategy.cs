namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Selects the single filesystem that contains a deployment's mutable game/runtime root.
/// </summary>
public enum ERuntimeStorageStrategy
{
    IncludedDisk = 0,
    AutomaticExternal = 1,
    FixedExternal = 2,
}
