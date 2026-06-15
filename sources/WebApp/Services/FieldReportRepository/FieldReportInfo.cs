namespace X39.UnitedTacticalForces.WebApp.Services.FieldReportRepository;

public record FieldReportInfo : FieldReportSummaryInfo
{
    public string Markdown { get; init; } = string.Empty;
}
