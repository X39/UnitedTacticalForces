using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Internal-protocol registration result. The credential is written only to the
/// private worker response header and must never be projected into browser DTOs.
/// </summary>
public sealed record WorkerRegistrationResult(
    WorkerRegistrationResponse Response,
    string WorkerCredential);
