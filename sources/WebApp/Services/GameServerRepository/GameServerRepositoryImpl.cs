using System.Net;
using System.Net.Http.Headers;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal sealed class GameServerRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IGameServerRepository
{
    public async Task<IReadOnlyList<GameServerInfo>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = await GetFromJsonAsync<GameServerInfo[]>("game-servers", cancellationToken)
            .ConfigureAwait(false);
        return servers ?? [];
    }

    public async Task<IReadOnlyList<GameServerCapabilityInfo>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var capabilities = await GetFromJsonAsync<GameServerCapabilityInfo[]>(
                "game-servers/capabilities",
                cancellationToken
            )
            .ConfigureAwait(false);
        return capabilities ?? [];
    }

    public async Task<IReadOnlyList<GameServerNotificationInfo>> GetNotificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var notifications = await GetFromJsonAsync<GameServerNotificationInfo[]>(
                "game-servers/notifications",
                cancellationToken
            )
            .ConfigureAwait(false);
        return notifications ?? [];
    }

    public async Task<GameServerInfo?> GetServerAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl($"game-servers/{serverId}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<GameServerInfo>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerInfo> CreateServerAsync(
        GameServerCreationInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync("game-servers", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<GameServerInfo>(response, "Server creation", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .DeleteAsync(BaseUrl.ResolveApiUrl($"game-servers/{serverId}"), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<LifecycleJobInfo>> GetJobsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var jobs = await GetFromJsonAsync<LifecycleJobInfo[]>(
                $"game-servers/{serverId}/jobs",
                cancellationToken
            )
            .ConfigureAwait(false);
        return jobs ?? [];
    }

    public async Task<LifecycleJobInfo> ExecuteActionAsync(
        Guid serverId,
        string actionId,
        string? confirmation = null,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                $"game-servers/{serverId}/actions/{Uri.EscapeDataString(actionId)}",
                new GameServerActionRequest { Confirmation = confirmation },
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<LifecycleJobInfo>(response, "Server action", cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<LifecycleJobInfo> RetryJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => SendJobInterventionAsync(jobId, "retry", cancellationToken);

    public Task<LifecycleJobInfo> SkipJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => SendJobInterventionAsync(jobId, "skip", cancellationToken);

    public async Task<IReadOnlyList<HostingFileInfo>> GetFilesAsync(
        Guid serverId,
        EHostingFileContext context,
        string path = "",
        CancellationToken cancellationToken = default)
    {
        var files = await GetFromJsonAsync<HostingFileInfo[]>(
                FileRoute(serverId, context, path),
                cancellationToken
            )
            .ConfigureAwait(false);
        return files ?? [];
    }

    public async Task<byte[]> GetFileContentAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(
                BaseUrl.ResolveApiUrl(FileRoute(serverId, context, path, "content")),
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<HostingFileInfo> UploadFileAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        using var body = new ByteArrayContent(content.ToArray());
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await HttpClient
            .PutAsync(
                BaseUrl.ResolveApiUrl(FileRoute(serverId, context, path)),
                body,
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<HostingFileInfo>(response, "File upload", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<HostingFileInfo> CreateDirectoryAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                FileBaseRoute(serverId, context),
                new DirectoryCreationRequest { Path = path },
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<HostingFileInfo>(response, "Directory creation", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteFileAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .DeleteAsync(BaseUrl.ResolveApiUrl(FileRoute(serverId, context, path)), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<PreservationSelectionInfo>> GetPreservationSelectionsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var selections = await GetFromJsonAsync<PreservationSelectionInfo[]>(
                $"game-servers/{serverId}/preservation-selections",
                cancellationToken
            )
            .ConfigureAwait(false);
        return selections ?? [];
    }

    public async Task<PreservationSelectionInfo> AddPreservationSelectionAsync(
        Guid serverId,
        PreservationSelectionInfoRequest payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                $"game-servers/{serverId}/preservation-selections",
                payload,
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<PreservationSelectionInfo>(
                response,
                "Preservation selection",
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task DeletePreservationSelectionAsync(
        Guid serverId,
        Guid selectionId,
        CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .DeleteAsync(
                BaseUrl.ResolveApiUrl($"game-servers/{serverId}/preservation-selections/{selectionId}"),
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<LifecycleJobInfo> SendJobInterventionAsync(
        Guid jobId,
        string intervention,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient
            .PostAsync(
                BaseUrl.ResolveApiUrl($"jobs/{jobId}/{intervention}"),
                content: null,
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<LifecycleJobInfo>(response, "Job intervention", cancellationToken)
            .ConfigureAwait(false);
    }

    private static string FileRoute(
        Guid serverId,
        EHostingFileContext context,
        string path = "",
        string? suffix = null)
    {
        var route = FileBaseRoute(serverId, context);
        if (!string.IsNullOrWhiteSpace(suffix))
            route = $"{route}/{suffix}";

        return $"{route}?path={Uri.EscapeDataString(path)}";
    }

    private static string FileBaseRoute(Guid serverId, EHostingFileContext context)
        => $"game-servers/{serverId}/files/{context.ToString().ToLowerInvariant()}";

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        return await ReadFromJsonAsync<T>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException($"{operation} returned no response body.");
    }

    private sealed record DirectoryCreationRequest
    {
        public string Path { get; init; } = string.Empty;
    }
}
