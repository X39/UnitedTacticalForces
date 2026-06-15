using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public record ModPackDownloadAuditInfo
{
    public PlainUserDto? User { get; init; }

    public long ModPackDefinitionId { get; init; }

    public long ModPackRevisionId { get; init; }

    public long ModPackRevisionDefinitionId { get; init; }

    public string ModPackRevisionDefinitionTitle { get; init; } = string.Empty;

    public DateTimeOffset ModPackRevisionTimeStampCreated { get; init; }

    public DateTimeOffset TimeStampDownloaded { get; init; }
}
