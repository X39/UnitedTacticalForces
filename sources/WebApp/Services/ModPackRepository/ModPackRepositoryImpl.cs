using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

[Scoped<ModPackRepositoryImpl, IModPackRepository>]
internal class ModPackRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IModPackRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ModCatalogItemInfo>> GetModsAsync(
        string gameIdentifier,
        string? search = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var route = $"game-content/mods?gameIdentifier={Uri.EscapeDataString(gameIdentifier)}"
                    + $"&skip={Math.Max(0, skip)}&take={Math.Clamp(take, 1, 100)}";
        if (!string.IsNullOrWhiteSpace(search))
            route += $"&search={Uri.EscapeDataString(search.Trim())}";
        return await GetFromJsonAsync<ModCatalogItemInfo[]>(route, cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModSourcePreviewInfo>> PreviewSourcesAsync(
        string gameIdentifier,
        IReadOnlyList<string> sources,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
            "game-content/mods/source-preview",
            new
            {
                GameIdentifier = gameIdentifier,
                Sources = sources.Select(source => new { Input = source }).ToArray(),
            },
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadFromJsonAsync<ModSourcePreviewInfo[]>(response, cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task<CreatedModResult> CreateModAsync(
        CreateModPayload payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync("game-content/mods", payload, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadFromJsonAsync<CreatedModResult>(response, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("The API did not return the created mod identifiers.");
    }

    /// <inheritdoc />
    public async Task<NormalizedModPackResult> CreateNormalizedModPackAsync(
        NormalizedModPackPayload payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync("game-content/mod-packs", payload, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadFromJsonAsync<NormalizedModPackResult>(response, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("The API did not return the created mod-pack identifiers.");
    }

    /// <inheritdoc />
    public async Task<long> AppendRevisionAsync(
        long modPackDefinitionId,
        NormalizedModPackRevisionPayload payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
            $"game-content/mod-packs/{modPackDefinitionId}/revisions",
            payload,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var result = await ReadFromJsonAsync<NormalizedModPackResult>(response, cancellationToken).ConfigureAwait(false);
        return result?.RevisionId
               ?? throw new InvalidOperationException("The API did not return the created revision identifier.");
    }

    /// <inheritdoc />
    public async Task<ModPackMembershipInfo> GetMembershipAsync(
        long revisionId,
        bool effective,
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<ModPackMembershipInfo>(
               $"game-content/mod-packs/revisions/{revisionId}/members?effective={effective.ToString().ToLowerInvariant()}",
               cancellationToken).ConfigureAwait(false)
           ?? new ModPackMembershipInfo();

    /// <inheritdoc />
    public async Task<ModPackFormatPreviewInfo> PreviewFormatAsync(string gameIdentifier, string formatIdentifier, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), "file", fileName);
        var response = await HttpClient.PostAsync(BaseUrl.ResolveApiUrl($"game-content/mod-packs/formats/{Uri.EscapeDataString(gameIdentifier)}/{Uri.EscapeDataString(formatIdentifier)}/preview"), form, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadFromJsonAsync<ModPackFormatPreviewInfo>(response, cancellationToken).ConfigureAwait(false) ?? new();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.ModPackFormatInfo>> GetRevisionFormatsAsync(long revisionId, CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.ModPackFormatInfo[]>($"game-content/mod-packs/revisions/{revisionId}/formats", cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<IReadOnlyList<X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.ModPackFormatInfo>> GetEventFormatsAsync(Guid eventId, CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.ModPackFormatInfo[]>($"events/{eventId}/mod-pack-formats", cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<CreatedModResult> CreateUploadedArtifactModAsync(string gameIdentifier, string title, X39.UnitedTacticalForces.Hosting.Contracts.EModUsageRole usageRole, string installerIdentifier, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(gameIdentifier), "gameIdentifier"); form.Add(new StringContent(title), "title"); form.Add(new StringContent(usageRole.ToString()), "usageRole"); form.Add(new StringContent(installerIdentifier), "installerIdentifier"); form.Add(new StreamContent(content), "file", fileName);
        var response = await HttpClient.PostAsync(BaseUrl.ResolveApiUrl("game-content/mods/uploaded-artifact"), form, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadFromJsonAsync<CreatedModResult>(response, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The API did not return the uploaded mod identifier.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NormalizedModPackListItemInfo>> GetNormalizedModPacksAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<NormalizedModPackListItemInfo[]>(
               $"game-content/mod-packs?gameIdentifier={Uri.EscapeDataString(gameIdentifier)}",
               cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public Task<NormalizedModPackWorkspaceInfo?> GetNormalizedModPackAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default)
        => GetFromJsonAsync<NormalizedModPackWorkspaceInfo>(
            $"game-content/mod-packs/{modPackDefinitionId}", cancellationToken);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(message) ? response.ReasonPhrase : message,
            null,
            response.StatusCode);
    }
}
