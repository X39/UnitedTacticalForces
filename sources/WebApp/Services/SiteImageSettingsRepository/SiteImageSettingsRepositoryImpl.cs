using System.Net;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.SiteImageSettingsRepository;

[Scoped<SiteImageSettingsRepositoryImpl, ISiteImageSettingsRepository>]
internal sealed class SiteImageSettingsRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), ISiteImageSettingsRepository
{
    public async Task<IReadOnlyList<SiteImageSettingInfo>> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetFromJsonAsync<SiteImageSettingInfo[]>("site-image-settings", cancellationToken)
            .ConfigureAwait(false);
        return settings ?? [];
    }

    public async Task<IReadOnlyList<SiteImageSettingInfo>> GetAdminSettingsAsync(
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var route = "site-image-settings/admin";
        if (!string.IsNullOrWhiteSpace(group))
            route = $"{route}?group={Uri.EscapeDataString(group.Trim())}";

        var settings = await GetFromJsonAsync<SiteImageSettingInfo[]>(route, cancellationToken)
            .ConfigureAwait(false);
        return settings ?? [];
    }

    public async Task<SiteImageSettingInfo> UpdateSettingAsync(
        string key,
        SiteImageSettingUpdateInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"site-image-settings/admin/{Uri.EscapeDataString(key)}",
                payload,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Site image setting was not found.");
        response.EnsureSuccessStatusCode();

        return await ReadFromJsonAsync<SiteImageSettingInfo>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Site image setting update returned no body.");
    }
}
