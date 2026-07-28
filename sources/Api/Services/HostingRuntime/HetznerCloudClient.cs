using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Narrow Hetzner Cloud API client for the dedicated server, Primary IPv4, and firewall resource graph.
/// Only the documented provider error code and message are retained from failure bodies. Request bodies and
/// successful response bodies are never included in diagnostics because they can contain bootstrap credentials.
/// </summary>
[Singleton<HetznerCloudClient>]
public sealed class HetznerCloudClient(
    IHttpClientFactory httpClientFactory,
    IHetznerRuntimeOptionsProvider optionsProvider)
{
    private const int PageSize = 50;
    private const int MaximumPageCount = 10_000;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    internal async Task<HetznerClientResult<IReadOnlyList<HetznerServerTypeDto>>> GetServerTypesAsync(
        CancellationToken cancellationToken)
    {
        var values = new List<HetznerServerTypeDto>();
        int? page = 1;
        var pagesRead = 0;
        while (page.HasValue)
        {
            pagesRead++;
            var response = await SendJsonAsync<HetznerServerTypesResponse>(
                    HttpMethod.Get,
                    $"server_types?page={page.Value}&per_page={PageSize}",
                    requestBody: null,
                    ambiguousMutation: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response.Outcome is not ERuntimeProviderOutcome.Succeeded || response.Value is null)
            {
                return HetznerClientResult<IReadOnlyList<HetznerServerTypeDto>>.Failure(
                    response.Outcome,
                    response.Error);
            }

            values.AddRange(response.Value.ServerTypes ?? []);
            var nextPage = response.Value.Meta?.Pagination?.NextPage;
            if (PaginationFailure(page.Value, nextPage, pagesRead) is { } paginationFailure)
            {
                return HetznerClientResult<IReadOnlyList<HetznerServerTypeDto>>.Failure(
                    ERuntimeProviderOutcome.Unknown,
                    paginationFailure);
            }
            page = nextPage;
        }

        return HetznerClientResult<IReadOnlyList<HetznerServerTypeDto>>.Success(values);
    }

    internal async Task<HetznerClientResult<IReadOnlyList<HetznerServer>>> GetServersAsync(
        IReadOnlyDictionary<string, string> requiredLabels,
        CancellationToken cancellationToken)
    {
        var values = new List<HetznerServer>();
        int? page = 1;
        var pagesRead = 0;
        while (page.HasValue)
        {
            pagesRead++;
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
            var nextPage = response.Value.Meta?.Pagination?.NextPage;
            if (PaginationFailure(page.Value, nextPage, pagesRead) is { } paginationFailure)
            {
                return HetznerClientResult<IReadOnlyList<HetznerServer>>.Failure(
                    ERuntimeProviderOutcome.Unknown,
                    paginationFailure);
            }
            page = nextPage;
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
        var pagesRead = 0;
        while (page.HasValue)
        {
            pagesRead++;
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
            var nextPage = response.Value.Meta?.Pagination?.NextPage;
            if (PaginationFailure(page.Value, nextPage, pagesRead) is { } paginationFailure)
            {
                return HetznerClientResult<IReadOnlyList<HetznerPrimaryIp>>.Failure(
                    ERuntimeProviderOutcome.Unknown,
                    paginationFailure);
            }
            page = nextPage;
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
        var pagesRead = 0;
        while (page.HasValue)
        {
            pagesRead++;
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
            var nextPage = response.Value.Meta?.Pagination?.NextPage;
            if (PaginationFailure(page.Value, nextPage, pagesRead) is { } paginationFailure)
            {
                return HetznerClientResult<IReadOnlyList<HetznerFirewall>>.Failure(
                    ERuntimeProviderOutcome.Unknown,
                    paginationFailure);
            }
            page = nextPage;
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
        var (operation, routeTemplate, resourceType, resourceId) = DescribeRequest(method, relativePath);
        using var activity = HostingTelemetry.StartProviderRequest(
            HetznerRuntimeProvider.Identifier,
            operation,
            method.Method,
            routeTemplate,
            resourceType,
            resourceId,
            ambiguousMutation);
        var stopwatch = Stopwatch.StartNew();
        HetznerHttpResult? result = null;
        int? statusCode = null;
        string? requestId = null;
        string? errorType = null;
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
            statusCode = (int) response.StatusCode;
            requestId = TryGetRequestId(response);
            if (response.IsSuccessStatusCode)
            {
                result = HetznerHttpResult.Success(response);
                return result;
            }

            var outcome = MapFailure(response.StatusCode, ambiguousMutation);
            errorType = $"http.{statusCode}";
            var sensitiveRequestValue = requestBody is HetznerServerCreateRequest serverRequest
                ? serverRequest.UserData
                : null;
            var error = await BuildFailureMessageAsync(
                    response,
                    options.Token,
                    sensitiveRequestValue,
                    cancellationToken)
                .ConfigureAwait(false);
            response.Dispose();
            result = HetznerHttpResult.Failure(outcome, error);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            errorType = "canceled";
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            errorType = typeof(TimeoutException).FullName;
            result = HetznerHttpResult.Failure(
                ambiguousMutation ? ERuntimeProviderOutcome.Unknown : ERuntimeProviderOutcome.TransientFailure,
                "Hetzner Cloud did not respond before the request timed out.");
            return result;
        }
        catch (HttpRequestException ex)
        {
            errorType = ex.GetType().FullName;
            result = HetznerHttpResult.Failure(
                ambiguousMutation ? ERuntimeProviderOutcome.Unknown : ERuntimeProviderOutcome.TransientFailure,
                "Hetzner Cloud could not be reached.");
            return result;
        }
        catch (InvalidOperationException ex)
        {
            errorType = ex.GetType().FullName;
            result = HetznerHttpResult.Failure(
                ERuntimeProviderOutcome.PermanentFailure,
                "The Hetzner Cloud client is not configured correctly.");
            return result;
        }
        catch (UriFormatException ex)
        {
            errorType = ex.GetType().FullName;
            result = HetznerHttpResult.Failure(
                ERuntimeProviderOutcome.PermanentFailure,
                "The Hetzner Cloud client is not configured correctly.");
            return result;
        }
        finally
        {
            stopwatch.Stop();
            HostingTelemetry.CompleteProviderRequest(
                activity,
                HetznerRuntimeProvider.Identifier,
                operation,
                result?.Outcome
                ?? (cancellationToken.IsCancellationRequested
                    ? ERuntimeProviderOutcome.TransientFailure
                    : ERuntimeProviderOutcome.Unknown),
                stopwatch.Elapsed.TotalMilliseconds,
                statusCode,
                requestId,
                errorType,
                result?.Error);
        }
    }

    private static string? PaginationFailure(int currentPage, int? nextPage, int pagesRead)
    {
        if (!nextPage.HasValue)
            return null;
        if (nextPage.Value <= currentPage)
            return "Hetzner Cloud returned a repeated or backward pagination cursor.";
        return pagesRead >= MaximumPageCount
            ? "Hetzner Cloud pagination exceeded the bounded discovery page limit."
            : null;
    }

    private static (string operation, string routeTemplate, string? resourceType, string? resourceId)
        DescribeRequest(HttpMethod method, string relativePath)
    {
        var path = relativePath.Split('?', 2)[0].Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resourceType = segments.FirstOrDefault() switch
        {
            "primary_ips" => "primary-ip",
            { Length: > 0 } value => value.TrimEnd('s'),
            _ => null,
        };
        var resourceId = segments.Length > 1 && long.TryParse(segments[1], out _)
            ? segments[1]
            : null;
        var operation = segments.Length > 3 && string.Equals(segments[2], "actions", StringComparison.Ordinal)
            ? segments[3]
            : method == HttpMethod.Post
                ? "create"
                : method == HttpMethod.Delete
                    ? "delete"
                    : resourceId is null ? "list" : "get";
        var routeTemplate = string.Join(
            '/',
            segments.Select(q => long.TryParse(q, out _) ? "{id}" : q));
        return (operation, routeTemplate, resourceType, resourceId);
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
        if (ambiguousMutation
            && (statusCode is HttpStatusCode.Conflict or HttpStatusCode.RequestTimeout
                || (int)statusCode >= 500))
            return ERuntimeProviderOutcome.Unknown;
        if (statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500)
        {
            return ERuntimeProviderOutcome.TransientFailure;
        }
        return ERuntimeProviderOutcome.PermanentFailure;
    }

    private static async Task<string> BuildFailureMessageAsync(
        HttpResponseMessage response,
        string token,
        string? sensitiveRequestValue,
        CancellationToken cancellationToken)
    {
        var summary = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "Hetzner Cloud rejected the configured credentials.",
            HttpStatusCode.NotFound => "The Hetzner Cloud resource was not found.",
            HttpStatusCode.TooManyRequests => "Hetzner Cloud rate-limited the request.",
            _ when (int)response.StatusCode >= 500 => "Hetzner Cloud is temporarily unavailable.",
            _ => $"Hetzner Cloud rejected the request with HTTP status {(int)response.StatusCode}.",
        };
        string? providerCode = null;
        string? providerMessage = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind is JsonValueKind.Object)
            {
                if (error.TryGetProperty("code", out var code) && code.ValueKind is JsonValueKind.String)
                    providerCode = code.GetString();
                if (error.TryGetProperty("message", out var message) && message.ValueKind is JsonValueKind.String)
                    providerMessage = message.GetString();
            }
        }
        catch (JsonException)
        {
            // The status remains actionable even when a proxy or provider returns a non-JSON failure body.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A broken error body must not hide the status code that triggered this diagnostic.
        }

        var requestId = TryGetRequestId(response);
        var detail = (providerCode, providerMessage) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $" Provider error {providerCode}: {providerMessage}.",
            ({ Length: > 0 }, _) => $" Provider error: {providerCode}.",
            (_, { Length: > 0 }) => $" Provider message: {providerMessage}.",
            _ => string.Empty,
        };
        var reference = string.IsNullOrWhiteSpace(requestId)
            ? string.Empty
            : $" Provider request: {requestId}.";
        return HostingFailureSanitizer.Sanitize(summary + detail + reference, token, sensitiveRequestValue);
    }

    private static string? TryGetRequestId(HttpResponseMessage response)
    {
        foreach (var name in new[] { "x-request-id", "request-id" })
        {
            if (response.Headers.TryGetValues(name, out var values))
                return values.FirstOrDefault();
        }
        return null;
    }

    private static HetznerServer MapServer(HetznerServerDto value)
        => new(
            value.Id,
            value.Name ?? string.Empty,
            value.Status ?? "unknown",
            value.PublicNet?.Ipv4?.Ip,
            value.PublicNet?.Ipv6?.Ip,
            ResourceReference(value.ServerType),
            ResourceReference(value.Image),
            ResourceReference(value.Location ?? value.Datacenter?.Location),
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
            (value.Rules ?? []).Select(MapFirewallRule).ToArray(),
            value.Rules is not null,
            CopyLabels(value.Labels));

    private static HetznerFirewallRule MapFirewallRule(HetznerFirewallRuleDto value)
        => new(
            value.Direction ?? string.Empty,
            value.Protocol ?? string.Empty,
            value.Port ?? string.Empty,
            value.SourceIps?.ToArray() ?? [],
            value.DestinationIps?.ToArray() ?? [],
            value.Description ?? string.Empty);

    private static HetznerResourceReference ResourceReference(HetznerResourceReferenceDto? value)
        => new(value?.Id is > 0 ? value.Id : null, value?.Name?.Trim() ?? string.Empty);

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
