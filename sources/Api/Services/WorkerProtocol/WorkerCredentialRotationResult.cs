using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Internal result whose plaintext credential is restricted to the private response header.
/// </summary>
public sealed record WorkerCredentialRotationResult(
    WorkerCredentialRotationResponse Response,
    string WorkerCredential);
