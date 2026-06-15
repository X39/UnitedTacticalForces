namespace X39.UnitedTacticalForces.WebApp.Services.MediaAssetRepository;

public record MediaAssetInfo
{
    public Guid PrimaryKey { get; init; }

    public string OriginalFileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long Size { get; init; }

    public string ImagePath { get; init; } = string.Empty;

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }

    public string? CreatedByName { get; init; }

    public string[] Tags { get; init; } = [];

    public MediaAssetUsageInfo[] Usages { get; init; } = [];
}
