namespace X39.UnitedTacticalForces.WebApp.Services.SiteImageSettingsRepository;

public record SiteImageSettingInfo
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public Guid ImageAssetFk { get; init; }

    public string ImagePath { get; init; } = string.Empty;

    public DateTimeOffset TimeStampCreated { get; init; }

    public DateTimeOffset TimeStampUpdated { get; init; }

    public string? UpdatedByName { get; init; }
}
