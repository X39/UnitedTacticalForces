namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents the aggregate Command Center snapshot for field report administration.
/// </summary>
public record CommandCenterFieldReportsSnapshotDto
{
    /// <summary>
    /// Total report records.
    /// </summary>
    public long FieldReportCount { get; init; }

    /// <summary>
    /// Total published, non-deleted reports.
    /// </summary>
    public long PublishedFieldReportCount { get; init; }

    /// <summary>
    /// Total draft, non-deleted reports.
    /// </summary>
    public long DraftFieldReportCount { get; init; }

    /// <summary>
    /// Total deleted reports.
    /// </summary>
    public long DeletedFieldReportCount { get; init; }

    /// <summary>
    /// Matching report rows for the current query.
    /// </summary>
    public CommandCenterFieldReportDto[] Reports { get; init; } = [];
}
