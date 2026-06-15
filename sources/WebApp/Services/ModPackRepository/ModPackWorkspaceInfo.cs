using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public record ModPackWorkspaceInfo
{
    public ModPackDefinitionDto Definition { get; init; } = new();

    public PlainUserDto? Owner { get; init; }

    public PlainModPackRevisionDto? LatestRevision { get; init; }

    public ModPackWorkspaceCompositionMemberInfo[] CompositionMembers { get; init; } = [];

    public PlainModPackRevisionDto[] RevisionPreview { get; init; } = [];

    public ModPackDownloadAuditInfo[] DownloadPreview { get; init; } = [];

    public long EventUsageCount { get; init; }

    public long DownloadRecordCount { get; init; }

    public long DownloadUserCount { get; init; }

    public DateTimeOffset? LastDownloaded { get; init; }

    public bool CanModify { get; init; }

    public bool CanDelete { get; init; }

    public bool CanViewLedgers { get; init; }
}
