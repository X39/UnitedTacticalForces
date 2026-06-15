namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record WorkerCommandInfo
{
    public Guid PrimaryKey { get; init; }
    public long Sequence { get; init; }
    public string ActionId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
    public DateTimeOffset TimeStampCreated { get; init; }
    public DateTimeOffset? TimeStampStarted { get; init; }
    public DateTimeOffset? TimeStampCompleted { get; init; }
}
