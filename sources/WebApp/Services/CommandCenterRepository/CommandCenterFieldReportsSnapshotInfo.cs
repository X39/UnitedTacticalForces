namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterFieldReportsSnapshotInfo
{
    public long FieldReportCount { get; init; }

    public long PublishedFieldReportCount { get; init; }

    public long DraftFieldReportCount { get; init; }

    public long DeletedFieldReportCount { get; init; }

    public CommandCenterFieldReportInfo[] Reports { get; init; } = [];
}
