namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Records a specific process incarnation attributed to one deployment workload.</summary>
/// <param name="ServerId">The durable server identity.</param>
/// <param name="DeploymentId">The deployment that owns the process.</param>
/// <param name="ProcessId">The operating-system process identifier.</param>
/// <param name="ProcessStartedAt">The process creation time used to detect PID reuse.</param>
/// <param name="ExecutablePath">The installed executable assigned to the workload.</param>
/// <param name="ServiceIdentifier">The workload identifier; omitted historical records describe the primary.</param>
/// <param name="ServiceOrdinal">The workload instance ordinal.</param>
/// <param name="LaunchId">The durable launch attempt, when recorded by a current worker.</param>
public sealed record Arma3ProcessIdentity(
    Guid ServerId,
    Guid DeploymentId,
    int ProcessId,
    DateTimeOffset ProcessStartedAt,
    string ExecutablePath,
    string ServiceIdentifier = "game-server",
    int ServiceOrdinal = 0,
    Guid? LaunchId = null);
