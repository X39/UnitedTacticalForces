using System.Net;
using X39.UnitedTacticalForces.Common.Verein;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

[Scoped<VereinRepositoryImpl, IVereinRepository>]
internal sealed class VereinRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IVereinRepository
{
    public async Task<VereinDocumentInfo?> GetPublishedSatzungAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl("verein/documents/satzung"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinDocumentInfo>(
                response,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VereinDocumentInfo>> GetPublishedMinutesAsync(
        int skip = 0,
        int take = 24,
        CancellationToken cancellationToken = default)
    {
        var documents = await GetFromJsonAsync<VereinDocumentInfo[]>(
                $"verein/documents/minutes?skip={skip}&take={take}",
                cancellationToken)
            .ConfigureAwait(false);
        return documents ?? [];
    }

    public async Task<VereinDocumentInfo?> GetPublishedMinuteAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"verein/documents/minutes/{documentId}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinDocumentInfo>(
                response,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<VereinSettingsInfo> GetSettingsAsync(CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<VereinSettingsInfo>("verein/settings", cancellationToken)
               .ConfigureAwait(false)
           ?? new VereinSettingsInfo();

    public async Task<VereinSettingsInfo> GetAdminSettingsAsync(CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<VereinSettingsInfo>("verein/admin/settings", cancellationToken)
               .ConfigureAwait(false)
           ?? new VereinSettingsInfo();

    public async Task<VereinSettingsInfo> UpdateSettingsAsync(
        VereinSettingsUpdateInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync("verein/admin/settings", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinSettingsInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Verein settings command returned no body.");
    }

    public async Task<IReadOnlyList<VereinDocumentInfo>> GetAdminDocumentsAsync(
        EVereinDocumentKind? kind = default,
        CancellationToken cancellationToken = default)
    {
        var route = "verein/admin/documents";
        if (kind is not null)
            route = $"{route}?kind={kind.Value}";
        var documents = await GetFromJsonAsync<VereinDocumentInfo[]>(route, cancellationToken)
            .ConfigureAwait(false);
        return documents ?? [];
    }

    public Task<VereinDocumentInfo> CreateDocumentAsync(
        EVereinDocumentKind kind,
        VereinDocumentRevisionUpsertInfo payload,
        CancellationToken cancellationToken = default)
        => SendDocumentAsync(HttpMethod.Post, $"verein/admin/documents/{kind}", payload, cancellationToken);

    public Task<VereinDocumentInfo> CreateDocumentRevisionAsync(
        long documentId,
        VereinDocumentRevisionUpsertInfo payload,
        CancellationToken cancellationToken = default)
        => SendDocumentAsync(HttpMethod.Post, $"verein/admin/documents/{documentId}/revisions", payload, cancellationToken);

    public Task<VereinDocumentInfo> PublishRevisionAsync(long revisionId, CancellationToken cancellationToken = default)
        => SendDocumentCommandAsync($"verein/admin/documents/revisions/{revisionId}/publish", cancellationToken);

    public Task<VereinDocumentInfo> UnpublishRevisionAsync(long revisionId, CancellationToken cancellationToken = default)
        => SendDocumentCommandAsync($"verein/admin/documents/revisions/{revisionId}/unpublish", cancellationToken);

    public async Task<IReadOnlyList<VereinMembershipInfo>> GetMembershipsAsync(
        int take = 100,
        string? search = default,
        CancellationToken cancellationToken = default)
    {
        var route = $"verein/memberships?take={take}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";
        var memberships = await GetFromJsonAsync<VereinMembershipInfo[]>(route, cancellationToken)
            .ConfigureAwait(false);
        return memberships ?? [];
    }

    public async Task<VereinMembershipInfo?> GetMembershipAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"verein/memberships/{userId:D}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinMembershipInfo>(
                response,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<VereinMembershipInfo> UpsertMembershipAsync(
        Guid userId,
        VereinMembershipUpdateInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync($"verein/memberships/{userId}", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinMembershipInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Verein membership command returned no body.");
    }

    public async Task<VereinMembershipRequestInfo?> GetMyMembershipRequestAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl("verein/membership-requests/me"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinMembershipRequestInfo>(
                response,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<VereinMembershipRequestInfo> CreateMembershipRequestAsync(
        VereinMembershipRequestCreateInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync("verein/membership-requests", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinMembershipRequestInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Verein membership request command returned no body.");
    }

    public Task<VereinMembershipRequestInfo> WithdrawMembershipRequestAsync(
        long requestId,
        CancellationToken cancellationToken = default)
        => SendMembershipRequestCommandAsync(
            $"verein/membership-requests/{requestId}/withdraw",
            payload: null,
            cancellationToken
        );

    public async Task<IReadOnlyList<VereinMembershipRequestInfo>> GetAdminMembershipRequestsAsync(
        EVereinMembershipRequestStatus? status = default,
        int take = 100,
        string? search = default,
        CancellationToken cancellationToken = default)
    {
        var route = $"verein/admin/membership-requests?take={take}";
        if (status is not null)
            route = $"{route}&status={Uri.EscapeDataString(status.Value.ToString())}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";
        var requests = await GetFromJsonAsync<VereinMembershipRequestInfo[]>(route, cancellationToken)
            .ConfigureAwait(false);
        return requests ?? [];
    }

    public Task<VereinMembershipRequestInfo> TransitionMembershipRequestAsync(
        long requestId,
        VereinMembershipRequestTransitionInfo payload,
        CancellationToken cancellationToken = default)
        => SendMembershipRequestCommandAsync(
            $"verein/admin/membership-requests/{requestId}/transition",
            payload,
            cancellationToken
        );

    public async Task<KassenbuchPageInfo> GetKassenbuchAsync(
        int skip = 0,
        int take = 50,
        DateTimeOffset? from = default,
        DateTimeOffset? to = default,
        string? search = default,
        string? category = default,
        CancellationToken cancellationToken = default)
    {
        var route = $"verein/kassenbuch?skip={skip}&take={take}";
        if (from is not null)
            route = $"{route}&from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        if (to is not null)
            route = $"{route}&to={Uri.EscapeDataString(to.Value.ToString("O"))}";
        if (!string.IsNullOrWhiteSpace(search))
            route = $"{route}&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(category))
            route = $"{route}&category={Uri.EscapeDataString(category)}";

        var page = await GetFromJsonAsync<KassenbuchPageInfo>(route, cancellationToken)
            .ConfigureAwait(false);
        return page ?? new KassenbuchPageInfo();
    }

    public Task<KassenbuchEntryInfo> CreateKassenbuchEntryAsync(
        KassenbuchEntryUpsertInfo payload,
        CancellationToken cancellationToken = default)
        => SendKassenbuchAsync(HttpMethod.Post, "verein/kassenbuch", payload, cancellationToken);

    public Task<KassenbuchEntryInfo> UpdateKassenbuchEntryAsync(
        long entryId,
        KassenbuchEntryUpsertInfo payload,
        CancellationToken cancellationToken = default)
        => SendKassenbuchAsync(HttpMethod.Put, $"verein/kassenbuch/{entryId}", payload, cancellationToken);

    public async Task<KassenbuchEntryInfo> DeleteKassenbuchEntryAsync(
        long entryId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .DeleteAsync(BaseUrl.ResolveApiUrl($"verein/kassenbuch/{entryId}"), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<KassenbuchEntryInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Kassenbuch command returned no body.");
    }

    private async Task<VereinDocumentInfo> SendDocumentAsync(
        HttpMethod method,
        string route,
        VereinDocumentRevisionUpsertInfo payload,
        CancellationToken cancellationToken)
    {
        var response = method == HttpMethod.Post
            ? await PostAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false)
            : await PutAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinDocumentInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Verein document command returned no body.");
    }

    private async Task<VereinMembershipRequestInfo> SendMembershipRequestCommandAsync(
        string route,
        object? payload,
        CancellationToken cancellationToken)
    {
        var response = payload is null
            ? await HttpClient.PostAsync(BaseUrl.ResolveApiUrl(route), content: null, cancellationToken)
                .ConfigureAwait(false)
            : await PostAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinMembershipRequestInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Verein membership request command returned no body.");
    }

    private async Task<VereinDocumentInfo> SendDocumentCommandAsync(
        string route,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient
            .PostAsync(BaseUrl.ResolveApiUrl(route), content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<VereinDocumentInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Verein document command returned no body.");
    }

    private async Task<KassenbuchEntryInfo> SendKassenbuchAsync(
        HttpMethod method,
        string route,
        KassenbuchEntryUpsertInfo payload,
        CancellationToken cancellationToken)
    {
        var response = method == HttpMethod.Post
            ? await PostAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false)
            : await PutAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<KassenbuchEntryInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Kassenbuch command returned no body.");
    }
}
