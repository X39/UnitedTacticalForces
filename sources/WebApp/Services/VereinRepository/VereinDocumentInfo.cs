using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public record VereinDocumentInfo
{
    public long PrimaryKey { get; init; }

    public EVereinDocumentKind Kind { get; init; }

    public bool IsDeleted { get; init; }

    public VereinDocumentRevisionInfo? Revision { get; init; }

    public VereinDocumentRevisionInfo[] Revisions { get; init; } = [];

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }
}
