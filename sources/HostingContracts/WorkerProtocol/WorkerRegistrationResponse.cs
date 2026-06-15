namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Non-secret worker registration result. The worker credential is returned only
/// through the <c>X-UTF-Worker-Credential</c> response header.
/// </summary>
public sealed record WorkerRegistrationResponse
{
    public Guid WorkerId { get; init; }

    public Guid DeploymentId { get; init; }

    public DateTimeOffset CredentialExpiresAt { get; init; }

    public int HeartbeatIntervalSeconds { get; init; }
}
