namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Non-secret metadata for an issued replacement credential. The credential itself is returned
/// only through the <c>X-UTF-Worker-Credential</c> response header.
/// </summary>
public sealed record WorkerCredentialRotationResponse
{
    public Guid WorkerId { get; init; }

    public Guid DeploymentId { get; init; }

    public DateTimeOffset CredentialExpiresAt { get; init; }
}
