using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Narrow Hetzner Cloud API client for the dedicated server, Primary IPv4, and firewall resource graph.
/// Provider response bodies are deliberately excluded from failures because they can contain user data.
/// </summary>
[Singleton<HetznerCloudClient>]
public sealed class HetznerCloudClient(
    IHttpClientFactory httpClientFactory,
    IHetznerRuntimeOptionsProvider optionsProvider)
{
    private const int PageSize = 50;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    internal async Task<HetznerClientResult<IReadOnlyList<HetznerServer>>> GetServersAsync(
        IReadOnlyDictionary<string, string> requiredLabels,
        CancellationToken cancellationToken)
    {
        var values = new List<HetznerServer>();
        int? page = 1;
        while (page.HasValue)
        {
            var response = await SendJsonAsync<HetznerServersResponse>(
                    HttpMethod.Get,
                    ListPath("servers", requiredLabels, page.Value),
                    requestBody: null,
                    ambiguousMutation: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Value is null)
            {
                return HetznerClientResult<IReadOnlyList<HetznerServer>>.Failure(
                    response.Outcome,
                    response.Error);
            }

            values.AddRange((response.Value.Servers ?? []).Select(MapServer));
            page = response.Value.Meta?.Pagination?.NextPage;
        }

        return HetznerClientResult<IReadOnlyList<HetznerServer>>.Success(values);
    }

    internal async Task<HetznerClientResult<HetznerServer>> GetServerAsync(
        long serverId,
        CancellationToken cancellationToken)
        => await GetSingleAsync<HetznerServerResponse, HetznerServer>(
                $"servers/{serverId}",
                static q => q.Server is null ? null : MapServer(q.Server),
                "server",
                cancellationToken)
            .ConfigureAwait(false);

    internal async Task<HetznerClientResult<HetznerServer>> CreateServerAsync(
        HetznerServerCreateRequest request,
        CancellationToken cancellationToken)
        => await CreateAsync<HetznerServerResponse, HetznerServer>(
                "servers",
                request,
                static q => q.Server is null ? null : MapServer(q.Server),
                static q => q.Id > 0,
                "server",
                cancellationToken)
            .ConfigureAwait(false);

    internal Task<HetznerClientResult> DeleteServerAsync(long serverId, CancellationToken cancellationToken)
        => DeleteAsync($"servers/{serverId}", cancellationToken);

    internal Task<HetznerClientResult> PowerOffServerAsync(long serverId, CancellationToken cancellationToken)
        => PostActionAsync($"servers/{serverId}/actions/poweroff", cancellationToken);

    internal async Task<HetznerClientResult<IReadOnlyList<HetznerPrimaryIp>>> GetPrimaryIpsAsync(
        IReadOnlyDictionary<string, string> requiredLabels,
        CancellationToken cancellationToken)
    {
        var values = new List<HetznerPrimaryIp>();
        int? page = 1;
        while (page.HasValue)
        {
            var response = await SendJsonAsync<HetznerPrimaryIpsResponse>(
                    HttpMethod.Get,
                    ListPath("primary_ips", requiredLabels, page.Value),
                    requestBody: null,
                    ambiguousMutation: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Value is null)
            {
                return HetznerClientResult<IReadOnlyList<HetznerPrimaryIp>>.Failure(
                    response.Outcome,
                    response.Error);
            }

            values.AddRange((response.Value.PrimaryIps ?? []).Select(MapPrimaryIp));
            page = response.Value.Meta?.Pagination?.NextPage;
        }

        return HetznerClientResult<IReadOnlyList<HetznerPrimaryIp>>.Success(values);
    }

    internal async Task<HetznerClientResult<HetznerPrimaryIp>> GetPrimaryIpAsync(
        long primaryIpId,
        CancellationToken cancellationToken)
        => await GetSingleAsync<HetznerPrimaryIpResponse, HetznerPrimaryIp>(
                $"primary_ips/{primaryIpId}",
                static q => q.PrimaryIp is null ? null : MapPrimaryIp(q.PrimaryIp),
                "Primary IP",
                cancellationToken)
            .ConfigureAwait(false);

    internal async Task<HetznerClientResult<HetznerPrimaryIp>> CreatePrimaryIpAsync(
        HetznerPrimaryIpCreateRequest request,
        CancellationToken cancellationToken)
        => await CreateAsync<HetznerPrimaryIpResponse, HetznerPrimaryIp>(
                "primary_ips",
                request,
                static q => q.PrimaryIp is null ? null : MapPrimaryIp(q.PrimaryIp),
                static q => q.Id > 0 && !string.IsNullOrWhiteSpace(q.Ip),
                "Primary IP",
                cancellationToken)
            .ConfigureAwait(false);

    internal Task<HetznerClientResult> UnassignPrimaryIpAsync(
        long primaryIpId,
        CancellationToken cancellationToken)
        => PostActionAsync($"primary_ips/{primaryIpId}/actions/unassign", cancellationToken);

    internal Task<HetznerClientResult> DeletePrimaryIpAsync(
        long primaryIpId,
        CancellationToken cancellationToken)
        => DeleteAsync($"primary_ips/{primaryIpId}", cancellationToken);

    internal async Task<HetznerClientResult<IReadOnlyList<HetznerFirewall>>> GetFirewallsAsync(
        IReadOnlyDictionary<string, string> requiredLabels,
        CancellationToken cancellationToken)
    {
        var values = new List<HetznerFirewall>();
        int? page = 1;
        while (page.HasValue)
        {
            var response = await SendJsonAsync<HetznerFirewallsResponse>(
                    HttpMethod.Get,
                    ListPath("firewalls", requiredLabels, page.Value),
                    requestBody: null,
                    ambiguousMutation: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Value is null)
            {
                return HetznerClientResult<IReadOnlyList<HetznerFirewall>>.Failure(
                    response.Outcome,
                    response.Error);
            }

            values.AddRange((response.Value.Firewalls ?? []).Select(MapFirewall));
            page = response.Value.Meta?.Pagination?.NextPage;
        }

        return HetznerClientResult<IReadOnlyList<HetznerFirewall>>.Success(values);
    }

    internal async Task<HetznerClientResult<HetznerFirewall>> GetFirewallAsync(
        long firewallId,
        CancellationToken cancellationToken)
        => await GetSingleAsync<HetznerFirewallResponse, HetznerFirewall>(
                $"firewalls/{firewallId}",
                static q => q.Firewall is null ? null : MapFirewall(q.Firewall),
                "firewall",
                cancellationToken)
            .ConfigureAwait(false);

    internal async Task<HetznerClientResult<HetznerFirewall>> CreateFirewallAsync(
        HetznerFirewallCreateRequest request,
        CancellationToken cancellationToken)
        => await CreateAsync<HetznerFirewallResponse, HetznerFirewall>(
                "firewalls",
                request,
                static q => q.Firewall is null ? null : MapFirewall(q.Firewall),
                static q => q.Id > 0,
                "firewall",
                cancellationToken)
            .ConfigureAwait(false);

    internal Task<HetznerClientResult> DeleteFirewallAsync(long firewallId, CancellationToken cancellationToken)
        => DeleteAsync($"firewalls/{firewallId}", cancellationToken);

    private async Task<HetznerClientResult<TValue>> GetSingleAsync<TResponse, TValue>(
        string relativePath,
        Func<TResponse, TValue?> selector,
        string resourceName,
        CancellationToken cancellationToken)
        where TResponse : class
        where TValue : class
    {
        var response = await SendJsonAsync<TResponse>(
                HttpMethod.Get,
                relativePath,
                requestBody: null,
                ambiguousMutation: false,
                cancellationToken)
            .ConfigureAwait(false);
        if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Value is null)
            return HetznerClientResult<TValue>.Failure(response.Outcome, response.Error);

        var value = selector(response.Value);
        return value is null
            ? HetznerClientResult<TValue>.Failure(
                ERuntimeProviderOutcome.Unknown,
                $"Hetzner Cloud returned an unreadable {resourceName} response.")
            : HetznerClientResult<TValue>.Success(value);
    }

    private async Task<HetznerClientResult<TValue>> CreateAsync<TResponse, TValue>(
        string relativePath,
        object request,
        Func<TResponse, TValue?> selector,
        Func<TValue, bool> hasIdentity,
        string resourceName,
        CancellationToken cancellationToken)
        where TResponse : class
        where TValue : class
    {
        var response = await SendJsonAsync<TResponse>(
                HttpMethod.Post,
                relativePath,
                request,
                ambiguousMutation: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Value is null)
            return HetznerClientResult<TValue>.Failure(response.Outcome, response.Error);

        var value = selector(response.Value);
        return value is not null && hasIdentity(value)
            ? HetznerClientResult<TValue>.Success(value)
            : HetznerClientResult<TValue>.Failure(
                ERuntimeProviderOutcome.Unknown,
                $"Hetzner Cloud accepted the create request but returned no usable {resourceName} identity.");
    }

    private async Task<HetznerClientResult> PostActionAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
                HttpMethod.Post,
                relativePath,
                requestBody: null,
                ambiguousMutation: true,
                cancellationToken)
            .ConfigureAwait(false);
        response.Response?.Dispose();
        return response.Outcome is ERuntimeProviderOutcome.Succeeded
            ? HetznerClientResult.Success()
            : HetznerClientResult.Failure(response.Outcome, response.Error);
    }

    private async Task<HetznerClientResult> DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
                HttpMethod.Delete,
                relativePath,
                requestBody: null,
                ambiguousMutation: false,
                cancellationToken)
            .ConfigureAwait(false);
        response.Response?.Dispose();
        return response.Outcome is ERuntimeProviderOutcome.Succeeded
            ? HetznerClientResult.Success()
            : HetznerClientResult.Failure(response.Outcome, response.Error);
    }

    private async Task<HetznerClientResult<TResponse>> SendJsonAsync<TResponse>(
        HttpMethod method,
        string relativePath,
        object? requestBody,
        bool ambiguousMutation,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var response = await SendAsync(
                method,
                relativePath,
                requestBody,
                ambiguousMutation,
                cancellationToken)
            .ConfigureAwait(false);
        if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Response is null)
            return HetznerClientResult<TResponse>.Failure(response.Outcome, response.Error);

        try
        {
            await using var stream = await response.Response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var value = await JsonSerializer.DeserializeAsync<TResponse>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            return value is null
                ? HetznerClientResult<TResponse>.Failure(
                    ERuntimeProviderOutcome.Unknown,
                    "Hetzner Cloud returned an unreadable response.")
                : HetznerClientResult<TResponse>.Success(value);
        }
        catch (JsonException)
        {
            return HetznerClientResult<TResponse>.Failure(
                ERuntimeProviderOutcome.Unknown,
                "Hetzner Cloud returned an unreadable response.");
        }
        finally
        {
            response.Response.Dispose();
        }
    }

    private async Task<HetznerHttpResult> SendAsync(
        HttpMethod method,
        string relativePath,
        object? requestBody,
        bool ambiguousMutation,
        CancellationToken cancellationToken)
    {
        var options = optionsProvider.Value;
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(options.GetBaseUri(), relativePath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (requestBody is not null)
                request.Content = JsonContent.Create(requestBody, options: SerializerOptions);

            var client = httpClientFactory.CreateClient(nameof(HetznerCloudClient));
            var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return HetznerHttpResult.Success(response);

            var outcome = MapFailure(response.StatusCode, ambiguousMutation);
            var error = BuildFailureMessage(response.StatusCode);
            response.Dispose();
            return HetznerHttpResult.Failure(outcome, error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HetznerHttpResult.Failure(
                ambiguousMutation ? ERuntimeProviderOutcome.Unknown : ERuntimeProviderOutcome.TransientFailure,
                "Hetzner Cloud did not respond before the request timed out.");
        }
        catch (HttpRequestException)
        {
            return HetznerHttpResult.Failure(
                ambiguousMutation ? ERuntimeProviderOutcome.Unknown : ERuntimeProviderOutcome.TransientFailure,
                "Hetzner Cloud could not be reached.");
        }
        catch (InvalidOperationException)
        {
            return HetznerHttpResult.Failure(
                ERuntimeProviderOutcome.PermanentFailure,
                "The Hetzner Cloud client is not configured correctly.");
        }
        catch (UriFormatException)
        {
            return HetznerHttpResult.Failure(
                ERuntimeProviderOutcome.PermanentFailure,
                "The Hetzner Cloud client is not configured correctly.");
        }
    }

    private static string ListPath(
        string resource,
        IReadOnlyDictionary<string, string> requiredLabels,
        int page)
    {
        var selector = string.Join(',', requiredLabels.Select(q => $"{q.Key}={q.Value}"));
        return $"{resource}?label_selector={Uri.EscapeDataString(selector)}&page={page}&per_page={PageSize}";
    }

    private static ERuntimeProviderOutcome MapFailure(HttpStatusCode statusCode, bool ambiguousMutation)
    {
        if (statusCode is HttpStatusCode.NotFound)
            return ERuntimeProviderOutcome.NotFound;
        if (ambiguousMutation && (statusCode is HttpStatusCode.Conflict || (int)statusCode >= 500))
            return ERuntimeProviderOutcome.Unknown;
        if (statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500)
        {
            return ERuntimeProviderOutcome.TransientFailure;
        }
        return ERuntimeProviderOutcome.PermanentFailure;
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "Hetzner Cloud rejected the configured credentials.",
            HttpStatusCode.NotFound => "The Hetzner Cloud resource was not found.",
            HttpStatusCode.TooManyRequests => "Hetzner Cloud rate-limited the request.",
            _ when (int)statusCode >= 500 => "Hetzner Cloud is temporarily unavailable.",
            _ => $"Hetzner Cloud rejected the request with HTTP status {(int)statusCode}.",
        };

    private static HetznerServer MapServer(HetznerServerDto value)
        => new(
            value.Id,
            value.Name ?? string.Empty,
            value.Status ?? "unknown",
            value.PublicNet?.Ipv4?.Ip,
            value.PublicNet?.Ipv6?.Ip,
            CopyLabels(value.Labels));

    private static HetznerPrimaryIp MapPrimaryIp(HetznerPrimaryIpDto value)
        => new(
            value.Id,
            value.Name ?? string.Empty,
            value.Ip ?? string.Empty,
            value.Type ?? "unknown",
            value.AssigneeId,
            value.AssigneeType,
            value.AutoDelete,
            CopyLabels(value.Labels));

    private static HetznerFirewall MapFirewall(HetznerFirewallDto value)
        => new(
            value.Id,
            value.Name ?? string.Empty,
            value.AppliedTo?.Count ?? 0,
            GetAppliedServerIds(value.AppliedTo),
            CopyLabels(value.Labels));

    private static IReadOnlyList<long> GetAppliedServerIds(IReadOnlyList<JsonElement>? appliedTo)
    {
        if (appliedTo is null)
            return [];

        var serverIds = new List<long>(appliedTo.Count);
        foreach (var target in appliedTo)
        {
            if (target.ValueKind is not JsonValueKind.Object
                || !target.TryGetProperty("type", out var type)
                || !string.Equals(type.GetString(), "server", StringComparison.OrdinalIgnoreCase)
                || !target.TryGetProperty("server", out var server)
                || server.ValueKind is not JsonValueKind.Object
                || !server.TryGetProperty("id", out var identifier)
                || !identifier.TryGetInt64(out var serverId))
            {
                continue;
            }
            serverIds.Add(serverId);
        }
        return serverIds;
    }

    private static IReadOnlyDictionary<string, string> CopyLabels(Dictionary<string, string>? labels)
        => labels is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(labels, StringComparer.Ordinal);

    private sealed record HetznerHttpResult(
        ERuntimeProviderOutcome Outcome,
        string? Error,
        HttpResponseMessage? Response)
    {
        public static HetznerHttpResult Success(HttpResponseMessage response)
            => new(ERuntimeProviderOutcome.Succeeded, null, response);

        public static HetznerHttpResult Failure(ERuntimeProviderOutcome outcome, string? error)
            => new(outcome, error, null);
    }
}

internal sealed record HetznerClientResult<T>(
    ERuntimeProviderOutcome Outcome,
    string? Error,
    T? Value)
{
    public static HetznerClientResult<T> Success(T value)
        => new(ERuntimeProviderOutcome.Succeeded, null, value);

    public static HetznerClientResult<T> Failure(ERuntimeProviderOutcome outcome, string? error)
        => new(outcome, error, default);
}