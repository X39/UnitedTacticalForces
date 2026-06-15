namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents the consolidated command-center workspace state for a mod pack.
/// </summary>
public record ModPackWorkspaceDto
{
    /// <summary>
    /// The mod pack definition and its latest active revision summary.
    /// </summary>
    public ModPackDefinitionDto Definition { get; init; } = new();

    /// <summary>
    /// The owner of the mod pack definition.
    /// </summary>
    public PlainUserDto? Owner { get; init; }

    /// <summary>
    /// The latest active revision for this workspace, if one exists.
    /// </summary>
    public PlainModPackRevisionDto? LatestRevision { get; init; }

    /// <summary>
    /// The linked standalone revisions when the mod pack is a composition.
    /// </summary>
    public ModPackWorkspaceCompositionMemberDto[] CompositionMembers { get; init; } = [];

    /// <summary>
    /// Recent revisions visible to operators with ledger access.
    /// </summary>
    public PlainModPackRevisionDto[] RevisionPreview { get; init; } = [];

    /// <summary>
    /// Recent download records visible to operators with ledger access.
    /// </summary>
    public ModPackDownloadAuditDto[] DownloadPreview { get; init; } = [];

    /// <summary>
    /// The number of events referencing revisions represented by this workspace.
    /// </summary>
    public long EventUsageCount { get; init; }

    /// <summary>
    /// The total number of download records for this mod pack definition.
    /// </summary>
    public long DownloadRecordCount { get; init; }

    /// <summary>
    /// The number of distinct users with download records for this mod pack definition.
    /// </summary>
    public long DownloadUserCount { get; init; }

    /// <summary>
    /// The latest recorded download timestamp for this mod pack definition.
    /// </summary>
    public Instant? LastDownloaded { get; init; }

    /// <summary>
    /// Whether the current user may update this mod pack.
    /// </summary>
    public bool CanModify { get; init; }

    /// <summary>
    /// Whether the current user may deactivate this mod pack.
    /// </summary>
    public bool CanDelete { get; init; }

    /// <summary>
    /// Whether the current user may view revision and download ledgers.
    /// </summary>
    public bool CanViewLedgers { get; init; }
}
