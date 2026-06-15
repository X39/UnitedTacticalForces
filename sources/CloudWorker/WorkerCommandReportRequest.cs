namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record WorkerCommandReportRequest(
    Guid DeploymentId,
    EWorkerCommandReportState State,
    string Stage,
    DateTimeOffset ObservedAt,
    string? ErrorCode,
    string? ErrorMessage,
    string? Output,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAt);