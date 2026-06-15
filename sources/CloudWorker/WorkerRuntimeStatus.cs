namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerRuntimeStatus(
    string State,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAt,
    string? Detail);