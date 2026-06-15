namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Secret-safe worker outcome persisted locally before it is reported.
/// </summary>
public sealed record WorkerCommandExecutionResult(
    bool Succeeded,
    string Stage,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? Output = null,
    int? ProcessId = null,
    DateTimeOffset? ProcessStartedAt = null)
{
    public static WorkerCommandExecutionResult Success(
        string stage,
        string? output = null,
        int? processId = null,
        DateTimeOffset? processStartedAt = null)
        => new(true, stage, Output: output, ProcessId: processId, ProcessStartedAt: processStartedAt);

    public static WorkerCommandExecutionResult Failure(string stage, string code, string message)
        => new(false, stage, code, message);
}