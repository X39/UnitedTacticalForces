namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Provider-facing bootstrap payload. The cloud-init string contains a short-lived secret and must never be logged.
/// </summary>
public sealed record WorkerBootstrapPayload(string CloudInit);
