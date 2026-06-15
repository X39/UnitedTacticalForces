namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Game-neutral classification applied to a server file or path.
/// </summary>
public enum EFileClassification
{
    Unknown = 0,
    GameFile = 1,
    SaveFile = 2,
    Log = 3,
    Configuration = 4,
    Mod = 5,
    Mission = 6,
    Upload = 7,
    Generated = 8,
    Downloaded = 9,
}
