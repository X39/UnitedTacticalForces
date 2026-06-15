namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Durable cleanup state for a provider resource that is UTF-managed but cannot be linked to a valid deployment.
/// </summary>
public enum EDanglingResourceIncidentStatus
{
    CleanupPending = 0,
    Releasing = 1,
    CleanupFailed = 2,
    Resolved = 3,
}
