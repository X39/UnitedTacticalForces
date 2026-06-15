namespace X39.UnitedTacticalForces.WebApp.Services.FieldReportRepository;

public record FieldReportSummaryInfo
{
    public Guid PrimaryKey { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Excerpt { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public Guid? HeroImageAssetFk { get; init; }

    public string? HeroImagePath { get; init; }

    public string? HeroImageAlt { get; init; }

    public bool IsFeatured { get; init; }

    public int SortOrder { get; init; }

    public DateTimeOffset? TimeStampPublished { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }
}
