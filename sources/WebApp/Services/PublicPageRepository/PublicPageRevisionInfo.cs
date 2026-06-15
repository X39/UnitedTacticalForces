namespace X39.UnitedTacticalForces.WebApp.Services.PublicPageRepository;

public record PublicPageRevisionInfo
{
    public long PrimaryKey { get; init; }

    public long PageFk { get; init; }

    public int Version { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string HeroKicker { get; init; } = string.Empty;

    public string HeroTitle { get; init; } = string.Empty;

    public string HeroDescription { get; init; } = string.Empty;

    public Guid HeroImageAssetFk { get; init; }

    public string HeroImagePath { get; init; } = string.Empty;

    public string Markdown { get; init; } = string.Empty;

    public string ContentJson { get; init; } = string.Empty;

    public string Comment { get; init; } = string.Empty;

    public bool IsPublished { get; init; }

    public DateTimeOffset? TimeStampPublished { get; init; }

    public DateTimeOffset TimeStampCreated { get; init; }

    public Guid? AuthorFk { get; init; }

    public string? AuthorName { get; init; }
}

