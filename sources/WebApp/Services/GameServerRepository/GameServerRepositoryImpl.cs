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

    public async Task<IReadOnlyList<ConfigurationProfileOptionInfo>> GetConfigurationProfileOptionsAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        var values = await GetFromJsonAsync<ConfigurationProfileOptionInfo[]>(
                $"configuration-profiles/options?gameIdentifier={Uri.EscapeDataString(gameIdentifier)}",
                cancellationToken)
            .ConfigureAwait(false);
        return values ?? [];
    }

    public async Task<IReadOnlyList<ConfigurationProfileInfo>> GetConfigurationProfilesAsync(
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<ConfigurationProfileInfo[]>("configuration-profiles", cancellationToken)
            .ConfigureAwait(false) ?? [];

    public async Task<ConfigurationProfileDefinitionCatalogInfo> GetConfigurationProfileDefinitionsAsync(
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<ConfigurationProfileDefinitionCatalogInfo>(
                "configuration-profiles/definitions", cancellationToken)
            .ConfigureAwait(false)
           ?? throw new InvalidOperationException("Configuration definitions returned no response body.");

    public async Task<ConfigurationProfileDetailsInfo> GetConfigurationProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<ConfigurationProfileDetailsInfo>(
                $"configuration-profiles/{profileId}", cancellationToken)
            .ConfigureAwait(false)
           ?? throw new InvalidOperationException("Configuration profile returned no response body.");

    public async Task<ConfigurationProfileAccessInfo> GetConfigurationProfileAccessAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<ConfigurationProfileAccessInfo>(
                $"configuration-profiles/{profileId}/access", cancellationToken)
            .ConfigureAwait(false)
           ?? throw new InvalidOperationException("Configuration-profile access returned no response body.");

    public async Task<ConfigurationProfileInfo> CreateConfigurationProfileAsync(
        string title,
        string gameIdentifier,
        IReadOnlyList<ConfigurationValueUpdateInfo> values,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                "configuration-profiles",
                new { Title = title, GameIdentifier = gameIdentifier, Values = values },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureConfigurationProfileSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadRequiredAsync<ConfigurationProfileInfo>(response, "Profile creation", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateConfigurationProfileRevisionAsync(
        Guid profileId,
        IReadOnlyList<ConfigurationValueUpdateInfo> values,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync(
                $"configuration-profiles/{profileId}/revisions",
                new { Values = values },
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureConfigurationProfileSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task ArchiveConfigurationProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync(
                BaseUrl.ResolveApiUrl($"configuration-profiles/{profileId}/archive"), null, cancellationToken)
            .ConfigureAwait(false);
        await EnsureConfigurationProfileSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public Task ChangeConfigurationProfileUserAccessAsync(
        Guid profileId, Guid userId, string permission, bool grant,
        CancellationToken cancellationToken = default)
        => ChangeConfigurationProfileAccessAsync(
            profileId, "users", new { UserId = userId, Permission = permission }, grant, cancellationToken);

    public Task ChangeConfigurationProfileRoleAccessAsync(
        Guid profileId, long roleId, string permission, bool grant,
        CancellationToken cancellationToken = default)
        => ChangeConfigurationProfileAccessAsync(
            profileId, "roles", new { RoleId = roleId, Permission = permission }, grant, cancellationToken);

    private async Task ChangeConfigurationProfileAccessAsync(
        Guid profileId, string subject, object payload, bool grant, CancellationToken cancellationToken)
    {
        var response = await PostAsJsonAsync(
                $"configuration-profiles/{profileId}/access/{subject}/{(grant ? "grant" : "revoke")}",
                payload,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureConfigurationProfileSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameConfigurationInfo> GetConfigurationAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
        => await GetFromJsonAsync<GameConfigurationInfo>(
                $"game-servers/{serverId}/configuration",
                cancellationToken)
            .ConfigureAwait(false)
           ?? throw new InvalidOperationException("Configuration returned no response body.");

    public async Task<GameConfigurationInfo> UpdateConfigurationAsync(
        Guid serverId,
        IReadOnlyList<ConfigurationValueUpdateInfo> values,
        IReadOnlyList<string> resetKeys,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"game-servers/{serverId}/configuration",
                new { Values = values, ResetKeys = resetKeys },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<GameConfigurationInfo>(response, "Configuration update", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SelectConfigurationProfileAsync(
        Guid serverId,
        Guid? profileId,
        Guid? pinnedRevisionId,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"game-servers/{serverId}/configuration/profile",
                new { ProfileId = profileId, PinnedRevisionId = pinnedRevisionId },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<GameServerModPackOptionInfo>> GetCreationModPackOptionsAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        var options = await GetFromJsonAsync<GameServerModPackOptionInfo[]>(
                $"game-servers/creation/mod-pack-options?gameIdentifier={Uri.EscapeDataString(gameIdentifier)}",
                cancellationToken)
            .ConfigureAwait(false);
        return options ?? [];
    }

    public async Task<IReadOnlyList<GameServerCostOwnerOptionInfo>> GetCreationCostOwnerOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await GetFromJsonAsync<GameServerCostOwnerOptionInfo[]>(
                "game-servers/creation/cost-owner-options",
                cancellationToken)
            .ConfigureAwait(false);
        return options ?? [];
    }

    public async Task<StorageDemandInfo> EstimateStorageDemandAsync(
        StorageDemandEstimateRequestInfo payload,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsJsonAsync("game-servers/storage-demand-estimate", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StorageDemandInfo>(response, "Storage estimate", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StorageDemandInfo> GetStorageDemandAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var value = await GetFromJsonAsync<StorageDemandInfo>(
                $"game-servers/{serverId}/storage-demand",
                cancellationToken)
            .ConfigureAwait(false);
        return value ?? throw new InvalidOperationException("Storage estimate returned no response body.");
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

    public async Task ChangeRuntimeProfileAsync(
        Guid serverId,
        string runtimeProfileIdentifier,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"game-servers/{serverId}/runtime-profile",
                new { RuntimeProfileIdentifier = runtimeProfileIdentifier },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeRuntimeStorageAsync(
        Guid serverId,
        string strategy,
        long? externalStorageSizeGiB,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"game-servers/{serverId}/runtime-storage",
                new { Strategy = strategy, ExternalStorageSizeGiB = externalStorageSizeGiB },
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<GameServerModPackOptionInfo>> GetModPackOptionsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var options = await GetFromJsonAsync<GameServerModPackOptionInfo[]>(
                $"game-servers/{serverId}/mod-pack-options",
                cancellationToken)
            .ConfigureAwait(false);
        return options ?? [];
    }

    public async Task ChangeModPackAsync(
        Guid serverId,
        long? modPackDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsJsonAsync(
                $"game-servers/{serverId}/mod-pack",
                new { ModPackDefinitionId = modPackDefinitionId },
                cancellationToken)
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

    public async Task<IReadOnlyList<HostingOperationInfo>> GetOperationsAsync(
        Guid serverId,
        int take = 80,
        CancellationToken cancellationToken = default)
    {
        var operations = await GetFromJsonAsync<HostingOperationInfo[]>(
                $"game-servers/{serverId}/operations?take={take}",
                cancellationToken
            )
            .ConfigureAwait(false);
        return operations ?? [];
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
        CancellationToken cancellationToken = default,
        bool confirmGeneratedOverride = false)
    {
        using var body = new ByteArrayContent(content.ToArray());
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await HttpClient
            .PutAsync(
                BaseUrl.ResolveApiUrl(FileRoute(serverId, context, path)
                                      + (confirmGeneratedOverride ? "&confirmGeneratedOverride=true" : string.Empty)),
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

    private static async Task EnsureConfigurationProfileSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        try
        {
            var error = await ReadFromJsonAsync<ErrorResponse>(response, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(error?.Error))
                throw new InvalidOperationException(error.Error);
        }
        catch (System.Text.Json.JsonException)
        {
            // Fall through to the status-specific message below.
        }

        throw new InvalidOperationException(response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You do not have permission to perform this configuration-profile action.",
            HttpStatusCode.Unauthorized => "Your session has expired. Sign in and try again.",
            HttpStatusCode.NotFound => "The configuration profile no longer exists.",
            _ => $"The configuration-profile request failed ({(int) response.StatusCode}).",
        });
    }

    private sealed record ErrorResponse(string? Error);

    private sealed record DirectoryCreationRequest
    {
        public string Path { get; init; } = string.Empty;
    }
}
