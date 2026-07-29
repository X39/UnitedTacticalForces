namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerRuntimeStatus(
    string State,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAt,
    string? Detail,
    long? DeploymentStorageTotalBytes = null,
    long? DeploymentStorageAvailableBytes = null,
    long? ControlledDeploymentBytes = null);
