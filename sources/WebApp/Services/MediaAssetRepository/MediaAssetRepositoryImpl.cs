using System.Net;
using System.Net.Http.Headers;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.MediaAssetRepository;

[Scoped<MediaAssetRepositoryImpl, IMediaAssetRepository>]
internal sealed class MediaAssetRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IMediaAssetRepository
{
    public async Task<IReadOnlyList<MediaAssetInfo>> GetAssetsAsync(
        MediaAssetFilterInfo? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new MediaAssetFilterInfo();
        var pageSize = Math.Clamp(filter.Take, 1, 200);
        var skip = filter.Skip;
        var results = new List<MediaAssetInfo>();
        while (true)
        {
            var page = await GetFromJsonAsync<MediaAssetInfo[]>(
                    CreateListRoute(filter with { Skip = skip, Take = pageSize }),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (page is null or { Length: 0 })
                break;

            results.AddRange(page);
            if (page.Length < pageSize)
                break;

            skip += page.Length;
        }

        return results;
    }

    public async Task<IReadOnlyList<MediaAssetInfo>> GetAssetPageAsync(
        MediaAssetFilterInfo? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new MediaAssetFilterInfo();
        var normalizedFilter = filter with
        {
            Skip = Math.Max(0, filter.Skip),
            Take = Math.Clamp(filter.Take, 1, 200),
        };
        var page = await GetFromJsonAsync<MediaAssetInfo[]>(
                CreateListRoute(normalizedFilter),
                cancellationToken
            )
            .ConfigureAwait(false);
        return page ?? [];
    }

    public async Task<MediaAssetInfo?> GetAssetAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"media-assets/{assetId:D}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<MediaAssetInfo>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MediaAssetInfo> UploadAssetAsync(
        Stream fileStream,
        string fileName,
        string fileMimeType,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        using var body = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(fileMimeType);
        body.Add(fileContent, "file", fileName);

        var tagValue = string.Join(",", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
        if (!string.IsNullOrWhiteSpace(tagValue))
            body.Add(new StringContent(tagValue), "tags");

        var response = await HttpClient
            .PostAsync(BaseUrl.ResolveApiUrl("media-assets/uploads"), body, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await ReadFromJsonAsync<MediaAssetInfo>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Media asset upload returned no body.");
    }

    public async Task<MediaAssetInfo> ReplaceTagsAsync(
        Guid assetId,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"media-assets/{assetId:D}/tags",
                new MediaAssetTagsUpdateInfo
                {
                    Tags = tags.ToArray(),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await ReadFromJsonAsync<MediaAssetInfo>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Media asset tag update returned no body.");
    }

    private static string CreateListRoute(MediaAssetFilterInfo filter)
    {
        var parameters = new List<string>
        {
            $"skip={filter.Skip}",
            $"take={filter.Take}",
        };
        AddParameter(parameters, "search", filter.Search);
        AddParameters(parameters, "tag", filter.IncludeTags);
        AddParameters(parameters, "excludeTag", filter.ExcludeTags);
        AddParameter(parameters, "usageStatus", filter.IncludeUsageStatus);
        AddParameter(parameters, "excludeUsageStatus", filter.ExcludeUsageStatus);
        AddParameters(parameters, "usageDomain", filter.IncludeUsageDomains);
        AddParameters(parameters, "excludeUsageDomain", filter.ExcludeUsageDomains);
        return $"media-assets?{string.Join("&", parameters)}";
    }

    private static void AddParameter(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }

    private static void AddParameters(List<string> parameters, string name, IEnumerable<string> values)
    {
        foreach (var value in values.Where(q => !string.IsNullOrWhiteSpace(q)))
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }
}
