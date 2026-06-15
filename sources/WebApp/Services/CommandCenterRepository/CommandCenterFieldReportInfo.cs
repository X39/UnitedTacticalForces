using X39.UnitedTacticalForces.WebApp.Services.FieldReportRepository;

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterFieldReportInfo : FieldReportInfo
{
    public bool IsPublished { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset TimeStampCreated { get; init; }

    public string? CreatedByName { get; init; }

    public string? UpdatedByName { get; init; }

    public int AuditRecordCount { get; init; }
}
