namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record StoredWorkerCredential(
    CloudWorkerConnectionConfig Connection,
    string Credential,
    DateTimeOffset ExpiresAt,
    int HeartbeatIntervalSeconds,
    DateTimeOffset? IssuedAt = null);