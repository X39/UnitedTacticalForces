namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Describes the expected provider-cost effect of an action.
/// </summary>
public enum ECostImpact
{
    NoCostImpact = 0,
    StartsCost = 1,
    StopsCost = 2,
    KeepsCostRunning = 3,
    Unknown = 4,
}
