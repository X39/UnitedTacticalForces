namespace X39.UnitedTacticalForces.WebApp.Services.FieldReportRepository;

public record FieldReportUpsertInfo
{
    public string? Slug { get; init; }

    public string? Title { get; init; }

    public string? Excerpt { get; init; }

    public string? Category { get; init; }

    public string? Markdown { get; init; }

    public Guid? HeroImageAssetFk { get; init; }

    public string? HeroImageAlt { get; init; }

    public bool IsFeatured { get; init; }

    public int SortOrder { get; init; }

    public bool IsPublished { get; init; }

    public DateTimeOffset? TimeStampPublished { get; init; }
}
