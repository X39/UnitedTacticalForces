namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Durable server record with its current lifecycle projection.
/// </summary>
public sealed record GameServerInfo
{
    public Guid PrimaryKey { get; init; }
    public string Title { get; init; } = string.Empty;
    public string GameIdentifier { get; init; } = string.Empty;
    public string RuntimeProviderIdentifier { get; init; } = string.Empty;
    public Guid CreatedById { get; init; }
    public Guid CostOwnerId { get; init; }
    public string CostOwnerName { get; init; } = string.Empty;
    public bool IsLaunchDisabled { get; init; }
    public bool IsArchived { get; init; }
    public bool CanModify { get; init; }
    public DateTimeOffset TimeStampCreated { get; init; }
    public DateTimeOffset TimeStampUpdated { get; init; }
    public GameServerDeploymentInfo? Deployment { get; init; }
    public int PendingJobCount { get; init; }
    public IReadOnlyCollection<GameServerActionInfo> Actions { get; init; } = [];
}
