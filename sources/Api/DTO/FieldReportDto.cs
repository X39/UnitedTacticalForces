namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a full public field report.
/// </summary>
public record FieldReportDto : FieldReportSummaryDto
{
    /// <summary>
    /// Markdown body rendered by the public detail page.
    /// </summary>
    public string Markdown { get; init; } = string.Empty;
}
