namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Requests the next stable command assigned to the authenticated worker.
/// </summary>
public sealed record WorkerCommandClaimRequest
{
    public Guid DeploymentId { get; init; }
}
