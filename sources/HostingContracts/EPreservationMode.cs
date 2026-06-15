namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Controls whether absent live files remove their persistent counterpart.
/// </summary>
public enum EPreservationMode
{
    CreateOrUpdate = 0,
    MirrorDeletions = 1,
}
