namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record Arma3ProcessIdentity(
    Guid ServerId,
    Guid DeploymentId,
    int ProcessId,
    DateTimeOffset ProcessStartedAt,
    string ExecutablePath);