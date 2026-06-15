namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents the aggregate Command Center snapshot for wiki and media administration.
/// </summary>
public record CommandCenterWikiMediaSnapshotDto
{
    /// <summary>
    /// Total amount of wiki page records.
    /// </summary>
    public long WikiPageCount { get; init; }

    /// <summary>
    /// Total amount of deleted wiki page records.
    /// </summary>
    public long DeletedWikiPageCount { get; init; }

    /// <summary>
    /// Total amount of wiki revision records.
    /// </summary>
    public long WikiRevisionCount { get; init; }

    /// <summary>
    /// Total amount of wiki audit records.
    /// </summary>
    public long WikiAuditRecordCount { get; init; }

    /// <summary>
    /// Matching wiki page rows for the current query.
    /// </summary>
    public CommandCenterWikiMediaPageDto[] Pages { get; init; } = [];
}
