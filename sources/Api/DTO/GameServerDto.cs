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
    public string RuntimeProfileIdentifier { get; init; } = string.Empty;
    public string RuntimeProfileDisplayName { get; init; } = string.Empty;
    public X39.UnitedTacticalForces.Hosting.Contracts.ERuntimeStorageStrategy RuntimeStorageStrategy { get; init; }
    public long? ExternalStorageSizeGiB { get; init; }
    public decimal? EstimatedHourlyCostEur { get; init; }
    public decimal? EstimatedMonthlyCostEur { get; init; }
    public long? SelectedModPackDefinitionId { get; init; }
    public string? SelectedModPackDefinitionTitle { get; init; }
    public long? PinnedModPackRevisionId { get; init; }
    public long? SelectedTerrainId { get; init; }
    public string? SelectedTerrainTitle { get; init; }
    public Guid? PinnedTerrainVersionId { get; init; }
    public Guid? SelectedScenarioId { get; init; }
    public string? SelectedScenarioTitle { get; init; }
    public Guid? PinnedScenarioVersionId { get; init; }
    public Guid? SelectedConfigurationProfileId { get; init; }
    public string? SelectedConfigurationProfileTitle { get; init; }
    public Guid? PinnedConfigurationProfileRevisionId { get; init; }
    public Guid CreatedById { get; init; }
    public Guid CostOwnerId { get; init; }
    public string CostOwnerName { get; init; } = string.Empty;
    public bool IsLaunchDisabled { get; init; }
    public bool IsArchived { get; init; }
    public bool CanModify { get; init; }
    public bool CanManageSecrets { get; init; }
    public bool CanRevealSecrets { get; init; }
    public Instant TimeStampCreated { get; init; }
    public Instant TimeStampUpdated { get; init; }
    public GameServerDeploymentDto? Deployment { get; init; }
    public int PendingJobCount { get; init; }
    public IReadOnlyCollection<GameServerActionDto> Actions { get; init; } = [];
}
