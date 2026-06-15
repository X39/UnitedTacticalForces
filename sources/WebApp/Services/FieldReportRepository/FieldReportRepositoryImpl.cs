using System.Net;
using X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.FieldReportRepository;

[Scoped<FieldReportRepositoryImpl, IFieldReportRepository>]
internal sealed class FieldReportRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IFieldReportRepository
{
    public async Task<IReadOnlyList<FieldReportSummaryInfo>> GetPublishedReportsAsync(
        int skip = 0,
        int take = 24,
        string? category = default,
        CancellationToken cancellationToken = default)
    {
        var route = $"field-reports?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(category))
            route = $"{route}&category={Uri.EscapeDataString(category)}";

        var reports = await GetFromJsonAsync<FieldReportSummaryInfo[]>(
                route,
                cancellationToken
            )
            .ConfigureAwait(false);
        return reports ?? [];
    }

    public async Task<FieldReportInfo?> GetPublishedReportAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"field-reports/{Uri.EscapeDataString(slug)}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<FieldReportInfo>(
                response,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<CommandCenterFieldReportInfo?> GetCommandCenterReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"field-reports/admin/{reportId}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<CommandCenterFieldReportInfo>(
                response,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public Task<CommandCenterFieldReportInfo> CreateReportAsync(
        FieldReportUpsertInfo payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Post, "field-reports", payload, cancellationToken);

    public Task<CommandCenterFieldReportInfo> UpdateReportAsync(
        Guid reportId,
        FieldReportUpsertInfo payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(HttpMethod.Put, $"field-reports/admin/{reportId}", payload, cancellationToken);

    public Task<CommandCenterFieldReportInfo> PublishReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendCommandAsync($"field-reports/admin/{reportId}/publish", cancellationToken);

    public Task<CommandCenterFieldReportInfo> UnpublishReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendCommandAsync($"field-reports/admin/{reportId}/unpublish", cancellationToken);

    public Task<CommandCenterFieldReportInfo> DeleteReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendDeleteAsync($"field-reports/admin/{reportId}", cancellationToken);

    public Task<CommandCenterFieldReportInfo> RestoreReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
        => SendCommandAsync($"field-reports/admin/{reportId}/restore", cancellationToken);

    private async Task<CommandCenterFieldReportInfo> SendJsonAsync(
        HttpMethod method,
        string route,
        FieldReportUpsertInfo payload,
        CancellationToken cancellationToken)
    {
        var response = method == HttpMethod.Post
            ? await PostAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false)
            : await PutAsJsonAsync(route, payload, cancellationToken)
                .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<CommandCenterFieldReportInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Field report command returned no body.");
    }

    private async Task<CommandCenterFieldReportInfo> SendCommandAsync(
        string route,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient
            .PostAsync(BaseUrl.ResolveApiUrl(route), content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<CommandCenterFieldReportInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Field report command returned no body.");
    }

    private async Task<CommandCenterFieldReportInfo> SendDeleteAsync(
        string route,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient
            .DeleteAsync(BaseUrl.ResolveApiUrl(route), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<CommandCenterFieldReportInfo>(
                   response,
                   cancellationToken
               )
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Field report command returned no body.");
    }
}
