namespace X39.UnitedTacticalForces.WebApp.Services.PublicPageRepository;

public record PublicPageRevisionUpsertInfo
{
    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string HeroKicker { get; init; } = string.Empty;

    public string HeroTitle { get; init; } = string.Empty;

    public string HeroDescription { get; init; } = string.Empty;

    public Guid HeroImageAssetFk { get; init; }

    public string Markdown { get; init; } = string.Empty;

    public string ContentJson { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public bool IsPublished { get; init; }
}

