using System.Net;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.PublicPageRepository;

[Scoped<PublicPageRepositoryImpl, IPublicPageRepository>]
internal sealed class PublicPageRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IPublicPageRepository
{
    public async Task<PublicPageInfo?> GetPublishedPageAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"public-pages/{Uri.EscapeDataString(slug)}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<PublicPageInfo>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PublicPageInfo>> GetAdminPagesAsync(CancellationToken cancellationToken = default)
    {
        var pages = await GetFromJsonAsync<PublicPageInfo[]>("public-pages/admin", cancellationToken)
            .ConfigureAwait(false);
        return pages ?? [];
    }

    public async Task<PublicPageInfo?> GetAdminPageAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"public-pages/admin/{Uri.EscapeDataString(slug)}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<PublicPageInfo>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PublicPageInfo> CreateRevisionAsync(
        string slug,
        PublicPageRevisionUpsertInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                $"public-pages/admin/{Uri.EscapeDataString(slug)}/revisions",
                payload,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<PublicPageInfo>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Public page command returned no body.");
    }

    public Task<PublicPageInfo> PublishRevisionAsync(
        long revisionId,
        CancellationToken cancellationToken = default)
        => SendRevisionCommandAsync($"public-pages/admin/revisions/{revisionId}/publish", cancellationToken);

    public Task<PublicPageInfo> UnpublishRevisionAsync(
        long revisionId,
        CancellationToken cancellationToken = default)
        => SendRevisionCommandAsync($"public-pages/admin/revisions/{revisionId}/unpublish", cancellationToken);

    private async Task<PublicPageInfo> SendRevisionCommandAsync(
        string route,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient
            .PostAsync(BaseUrl.ResolveApiUrl(route), content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<PublicPageInfo>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Public page command returned no body.");
    }
}
