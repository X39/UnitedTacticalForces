using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.RoleRepository;

[Scoped<RoleRepositoryImpl, IRoleRepository>]
internal sealed class RoleRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IRoleRepository
{
    public async Task<RoleMemberSnapshotInfo> GetRoleMembersAsync(
        long roleId,
        int take = 160,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var route = $"command-center/authority-role/{roleId}/members?take={take}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";

        var snapshot = await GetFromJsonAsync<RoleMemberSnapshotInfo>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return snapshot ?? new RoleMemberSnapshotInfo();
    }

    public async Task CreateRoleAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [nameof(title)]       = title,
                [nameof(description)] = description,
            }
        );
        var response = await HttpClient
            .PostAsync(BaseUrl.ResolveApiUrl("roles/create/role"), content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateRoleAsync(
        long roleId,
        string title,
        string description,
        CancellationToken cancellationToken = default
    )
    {
        var response = await PostAsJsonAsync(
                $"roles/update/role/{roleId}",
                new RoleMetadataUpdateInfo
                {
                    Title       = title,
                    Description = description,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .PostAsync(BaseUrl.ResolveApiUrl($"roles/delete/role/{roleId}"), content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetUserRoleAsync(
        Guid userId,
        long roleId,
        bool roleActive,
        CancellationToken cancellationToken = default
    )
    {
        var response = await HttpClient
            .PostAsync(
                BaseUrl.ResolveApiUrl(
                    $"roles/set/user/{userId:D}/role/{roleId}/to/{roleActive.ToString().ToLowerInvariant()}"
                ),
                content: null,
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetRoleClaimAsync(
        long roleId,
        long claimId,
        bool claimActive,
        CancellationToken cancellationToken = default
    )
    {
        var response = await HttpClient
            .PostAsync(
                BaseUrl.ResolveApiUrl(
                    $"roles/set/role/{roleId}/claim/{claimId}/to/{claimActive.ToString().ToLowerInvariant()}"
                ),
                content: null,
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private sealed record RoleMetadataUpdateInfo
    {
        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;
    }
}
