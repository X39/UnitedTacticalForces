using X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

namespace X39.UnitedTacticalForces.WebApp.Services.FieldReportRepository;

public interface IFieldReportRepository
{
    Task<IReadOnlyList<FieldReportSummaryInfo>> GetPublishedReportsAsync(
        int skip = 0,
        int take = 24,
        string? category = default,
        CancellationToken cancellationToken = default);

    Task<FieldReportInfo?> GetPublishedReportAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo?> GetCommandCenterReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo> CreateReportAsync(
        FieldReportUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo> UpdateReportAsync(
        Guid reportId,
        FieldReportUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo> PublishReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo> UnpublishReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo> DeleteReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<CommandCenterFieldReportInfo> RestoreReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);
}
