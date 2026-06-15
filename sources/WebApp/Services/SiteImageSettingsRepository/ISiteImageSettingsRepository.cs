namespace X39.UnitedTacticalForces.WebApp.Services.SiteImageSettingsRepository;

public interface ISiteImageSettingsRepository
{
    Task<IReadOnlyList<SiteImageSettingInfo>> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SiteImageSettingInfo>> GetAdminSettingsAsync(
        string? group = null,
        CancellationToken cancellationToken = default);

    Task<SiteImageSettingInfo> UpdateSettingAsync(
        string key,
        SiteImageSettingUpdateInfo payload,
        CancellationToken cancellationToken = default);
}
