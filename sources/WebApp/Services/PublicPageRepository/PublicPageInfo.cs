namespace X39.UnitedTacticalForces.WebApp.Services.PublicPageRepository;

public record PublicPageInfo
{
    public long PrimaryKey { get; init; }

    public string Slug { get; init; } = string.Empty;

    public bool IsDeleted { get; init; }

    public PublicPageRevisionInfo? Revision { get; init; }

    public PublicPageRevisionInfo[] Revisions { get; init; } = [];

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }
}
