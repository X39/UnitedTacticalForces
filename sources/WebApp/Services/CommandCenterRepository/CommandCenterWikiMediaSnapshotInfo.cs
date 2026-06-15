namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterWikiMediaSnapshotInfo
{
    public long WikiPageCount { get; init; }

    public long DeletedWikiPageCount { get; init; }

    public long WikiRevisionCount { get; init; }

    public long WikiAuditRecordCount { get; init; }

    public CommandCenterWikiMediaPageInfo[] Pages { get; init; } = [];
}
