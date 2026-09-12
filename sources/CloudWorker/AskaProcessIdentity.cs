namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Persisted identity and readiness evidence for one ASKA Wine process group.</summary>
/// <param name="ServerId">Owning game-server identifier.</param>
/// <param name="DeploymentId">Owning deployment identifier.</param>
/// <param name="ProcessId">Wine launcher process and session identifier.</param>
/// <param name="ProcessStartedAt">Observed operating-system start time.</param>
/// <param name="ExecutablePath">Attributed ASKA executable path.</param>
/// <param name="Ready">Whether ASKA passed readiness checks.</param>
public sealed record AskaProcessIdentity(
    Guid ServerId,
    Guid DeploymentId,
    int ProcessId,
    DateTimeOffset ProcessStartedAt,
    string ExecutablePath,
    bool Ready);
