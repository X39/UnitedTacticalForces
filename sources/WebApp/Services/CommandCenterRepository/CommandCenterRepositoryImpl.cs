using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

[Scoped<CommandCenterRepositoryImpl, ICommandCenterRepository>]
internal sealed class CommandCenterRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), ICommandCenterRepository
{
    public async Task<CommandCenterCommunitySnapshotInfo> GetCommunitySnapshotAsync(
        int take = 80,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var route = $"command-center/community-snapshot?take={take}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";

        var snapshot = await GetFromJsonAsync<CommandCenterCommunitySnapshotInfo>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new CommandCenterCommunitySnapshotInfo();
    }

    public async Task<CommandCenterAuthoritySnapshotInfo> GetAuthoritySnapshotAsync(
        int take = 120,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var route = $"command-center/authority-snapshot?take={take}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";

        var snapshot = await GetFromJsonAsync<CommandCenterAuthoritySnapshotInfo>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new CommandCenterAuthoritySnapshotInfo();
    }

    public async Task<CommandCenterWikiMediaSnapshotInfo> GetWikiMediaSnapshotAsync(
        int take = 80,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var route = $"command-center/wiki-media-snapshot?take={take}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";

        var snapshot = await GetFromJsonAsync<CommandCenterWikiMediaSnapshotInfo>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new CommandCenterWikiMediaSnapshotInfo();
    }

    public async Task<CommandCenterFieldReportsSnapshotInfo> GetFieldReportsSnapshotAsync(
        int take = 120,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var route = $"command-center/field-reports-snapshot?take={take}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";

        var snapshot = await GetFromJsonAsync<CommandCenterFieldReportsSnapshotInfo>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new CommandCenterFieldReportsSnapshotInfo();
    }

    public async Task<CommandCenterSettingsSnapshotInfo> GetSettingsSnapshotAsync(
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await GetFromJsonAsync<CommandCenterSettingsSnapshotInfo>(
                "command-center/settings-snapshot",
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new CommandCenterSettingsSnapshotInfo();
    }

    public async Task<CommandCenterEventAdminSnapshotInfo> GetEventAdminSnapshotAsync(
        int skip = 0,
        int take = 80,
        bool hostedByMeOnly = false,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var route = $"command-center/event-admin-snapshot?skip={skip}&take={take}&hostedByMeOnly={hostedByMeOnly}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";

        var snapshot = await GetFromJsonAsync<CommandCenterEventAdminSnapshotInfo>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new CommandCenterEventAdminSnapshotInfo();
    }
}
