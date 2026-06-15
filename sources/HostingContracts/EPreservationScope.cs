namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Controls which live files a preservation selection matches.
/// </summary>
public enum EPreservationScope
{
    File = 0,
    FlatFolder = 1,
    RecursiveFolder = 2,
}
