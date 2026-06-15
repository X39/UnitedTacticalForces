namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Last reconciled allocation state of a deployment resource.
/// </summary>
public enum EDeploymentResourceState
{
    Provisioning = 0,
    Allocated = 1,
    ReleasePending = 2,
    Releasing = 3,
    Released = 4,
    Unknown = 5,
    Failed = 6,
}
