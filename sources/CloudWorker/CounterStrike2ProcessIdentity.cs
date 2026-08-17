namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record CounterStrike2ProcessIdentity(
    Guid ServerId,
    Guid DeploymentId,
    int ProcessId,
    DateTimeOffset ProcessStartedAt,
    string ExecutablePath);
