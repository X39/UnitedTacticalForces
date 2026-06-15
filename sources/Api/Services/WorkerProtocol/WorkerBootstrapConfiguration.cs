namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// One-time provisioning data for cloud-init or an equivalent provider bootstrap channel.
/// The plaintext credential exists only in this in-memory result and is never persisted.
/// </summary>
public sealed record WorkerBootstrapConfiguration(
    Guid WorkerId,
    Guid DeploymentId,
    string BootstrapCredential,
    DateTimeOffset ExpiresAt,
    string RegistrationPath);
