namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// In-memory result returned when a deployment-scoped worker credential is issued.
/// </summary>
public sealed record WorkerCredentialIssue(string Credential, DateTimeOffset ExpiresAt);
