namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Deployment attribution required when an external worker requests a replacement credential.
/// The current credential is carried only in the Authorization header.
/// </summary>
public sealed record WorkerCredentialRotationRequest
{
    public Guid DeploymentId { get; init; }
}
