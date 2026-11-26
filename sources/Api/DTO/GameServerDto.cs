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

    /// <summary>Identifier of the user behind the cost owner when the owner is a user and the viewer may see the cost owner; otherwise null.</summary>
    /// <value>The paying user's identifier, or <see langword="null"/> for non-user owners and owners hidden from the viewer.</value>
    public Guid? CostOwnerUserId { get; init; }
    public bool IsLaunchDisabled { get; init; }

    /// <summary>Ascending cost alert amounts of the server's current cost owner when the viewer may see that owner; otherwise null.</summary>
    /// <value>The alert amounts, an empty list when the owner has none, or <see langword="null"/> when the owner is hidden from the viewer.</value>
    public IReadOnlyList<decimal>? CostAlertThresholds { get; init; }

    /// <summary>Whether the viewer may change the current cost owner's cost alert settings; null when the viewer may not see that owner.</summary>
    /// <value><see langword="true"/> or <see langword="false"/> for viewers who may see the owner, otherwise <see langword="null"/>.</value>
    public bool? CanManageCostAlerts { get; init; }

    /// <summary>Kinds of applied change that still wait for the next start, in the order mod pack, configuration, credentials.</summary>
    /// <value>The pending areas, or an empty list when nothing waits for a start.</value>
    public IReadOnlyList<X39.UnitedTacticalForces.Hosting.Contracts.EGameServerChangeArea> ChangesPendingStartAreas { get; init; } = [];

    /// <summary>Whether at least one applied change still waits for the next start.</summary>
    /// <value><see langword="true"/> when <see cref="ChangesPendingStartAreas"/> is not empty.</value>
    public bool HasChangesPendingStart { get; init; }
    public bool IsArchived { get; init; }
    public bool CanModify { get; init; }
    public bool CanManageSecrets { get; init; }

    /// <summary>Whether the viewer may list, grant and revoke access to this server; the exact rule of the manage-access requirement.</summary>
    /// <value><see langword="true"/> for a global or hosting administrator, a server-wide holder and a holder of the manage-access permission for this server.</value>
    public bool CanManageAccess { get; init; }

    /// <summary>Whether the current cost owner rejects new cost-generating assignments, when the viewer may see the cost owner; otherwise null.</summary>
    /// <value>The frozen flag of the current owner, or <see langword="null"/> when the owner is hidden from the viewer.</value>
    public bool? CostOwnerIsFrozen { get; init; }

    /// <summary>When the current cost owner was assigned to the server; filled for the server detail only and only when the viewer may see the cost owner.</summary>
    /// <value>The assignment time of the open cost owner assignment, or <see langword="null"/> in the list, when the owner is hidden from the viewer or when no open assignment exists.</value>
    public Instant? CostOwnerAssignedAt { get; init; }
    public bool CanRevealSecrets { get; init; }
    public Instant TimeStampCreated { get; init; }
    public Instant TimeStampUpdated { get; init; }
    public GameServerDeploymentDto? Deployment { get; init; }
    public int PendingJobCount { get; init; }
    public IReadOnlyCollection<GameServerActionDto> Actions { get; init; } = [];

    /// <summary>The automation settings, limits, live status and start consent of the server; part of the server detail only.</summary>
    /// <value>The automation block, or <see langword="null"/> in the fleet list and notifications, and when the game definition is unknown.</value>
    public ServerAutomationDto? Automation { get; init; }

    /// <summary>Whether the viewer may change the automation settings: server modify permission plus the right to use the current cost owner.</summary>
    /// <value><see langword="true"/> or <see langword="false"/> in the server detail, <see langword="null"/> where the flag is not computed (fleet list and notifications).</value>
    public bool? CanManageAutomation { get; init; }
}
