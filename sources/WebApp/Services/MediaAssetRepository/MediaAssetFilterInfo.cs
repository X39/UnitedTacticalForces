namespace X39.UnitedTacticalForces.WebApp.Services.MediaAssetRepository;

public record MediaAssetFilterInfo
{
    public string? Search { get; init; }

    public string[] IncludeTags { get; init; } = [];

    public string[] ExcludeTags { get; init; } = [];

    public string? IncludeUsageStatus { get; init; }

    public string? ExcludeUsageStatus { get; init; }

    public string[] IncludeUsageDomains { get; init; } = [];

    public string[] ExcludeUsageDomains { get; init; } = [];

    public int Skip { get; init; }

    public int Take { get; init; } = 120;
}
