namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Durable server record plus its current externally useful lifecycle projection.
/// </summary>
public sealed record GameServerDto
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
    public Instant TimeStampCreated { get; init; }
    public Instant TimeStampUpdated { get; init; }
    public GameServerDeploymentDto? Deployment { get; init; }
    public int PendingJobCount { get; init; }
    public IReadOnlyCollection<GameServerActionDto> Actions { get; init; } = [];
}
