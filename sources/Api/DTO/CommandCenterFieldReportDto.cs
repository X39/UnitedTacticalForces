namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents an editable field report row for Command Center.
/// </summary>
public record CommandCenterFieldReportDto : FieldReportDto
{
    /// <summary>
    /// Whether this report is visible publicly.
    /// </summary>
    public bool IsPublished { get; init; }

    /// <summary>
    /// Whether this report has been removed from the active editorial workflow.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Display name of the creating user.
    /// </summary>
    public string? CreatedByName { get; init; }

    /// <summary>
    /// Display name of the last updating user.
    /// </summary>
    public string? UpdatedByName { get; init; }

    /// <summary>
    /// Number of audit records attached to this report.
    /// </summary>
    public int AuditRecordCount { get; init; }
}
