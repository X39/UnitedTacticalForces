using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Provider-neutral tracing and metrics for cloud runtime operations.
/// Values recorded here must remain credential- and bootstrap-safe.
/// </summary>
internal static class HostingTelemetry
{
    public const string SourceName = "X39.UnitedTacticalForces.Hosting";

    private static readonly ActivitySource ActivitySource = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> ProviderRequestCount = Meter.CreateCounter<long>(
        "hosting.provider.request.count",
        "{request}",
        "Cloud-provider requests issued by the hosting control plane.");
    private static readonly Histogram<double> ProviderRequestDuration = Meter.CreateHistogram<double>(
        "hosting.provider.request.duration",
        "ms",
        "Cloud-provider request duration.");

    public static Activity? StartProviderRequest(
        string provider,
        string operation,
        string method,
        string routeTemplate,
        string? resourceType,
        string? resourceId,
        bool ambiguousMutation)
    {
        var activity = ActivitySource.StartActivity(
            $"{provider}.{operation}",
            ActivityKind.Client);
        activity?.SetTag("utf.hosting.provider", provider);
        activity?.SetTag("utf.hosting.operation", operation);
        activity?.SetTag("utf.hosting.resource.type", resourceType);
        activity?.SetTag("utf.hosting.resource.id", resourceId);
        activity?.SetTag("utf.hosting.mutation.ambiguous_on_failure", ambiguousMutation);
        activity?.SetTag("http.request.method", method);
        activity?.SetTag("url.template", routeTemplate);
        return activity;
    }

    public static void CompleteProviderRequest(
        Activity? activity,
        string provider,
        string operation,
        ERuntimeProviderOutcome outcome,
        double elapsedMilliseconds,
        int? statusCode,
        string? requestId,
        string? errorType,
        string? error)
    {
        activity?.SetTag("utf.hosting.outcome", outcome.ToString());
        activity?.SetTag("http.response.status_code", statusCode);
        activity?.SetTag("utf.hosting.provider.request_id", requestId);
        activity?.SetTag("error.type", errorType);
        if (outcome is not ERuntimeProviderOutcome.Succeeded)
            activity?.SetStatus(ActivityStatusCode.Error, Limit(error, 512));

        var tags = new TagList
        {
            { "utf.hosting.provider", provider },
            { "utf.hosting.operation", operation },
            { "utf.hosting.outcome", outcome.ToString() },
        };
        ProviderRequestCount.Add(1, tags);
        ProviderRequestDuration.Record(elapsedMilliseconds, tags);
    }

    public static Activity? StartRuntimeOperation(
        string provider,
        string operation,
        Guid? serverId = null,
        Guid? deploymentId = null,
        string? resourceType = null,
        string? resourceId = null)
    {
        var activity = ActivitySource.StartActivity(
            $"runtime.{operation}",
            ActivityKind.Internal);
        activity?.SetTag("utf.hosting.provider", provider);
        activity?.SetTag("utf.hosting.operation", operation);
        activity?.SetTag("utf.hosting.server.id", serverId);
        activity?.SetTag("utf.hosting.deployment.id", deploymentId);
        activity?.SetTag("utf.hosting.resource.type", resourceType);
        activity?.SetTag("utf.hosting.resource.id", resourceId);
        return activity;
    }

    public static void CompleteRuntimeOperation(
        Activity? activity,
        ERuntimeProviderOutcome outcome,
        string stage,
        string? error)
    {
        activity?.SetTag("utf.hosting.outcome", outcome.ToString());
        activity?.SetTag("utf.hosting.stage", stage);
        if (outcome is not ERuntimeProviderOutcome.Succeeded)
            activity?.SetStatus(ActivityStatusCode.Error, Limit(error, 512));
    }

    public static void RecordException(Activity? activity, Exception exception, string? safeMessage = null)
    {
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, Limit(safeMessage ?? exception.Message, 512));
    }

    private static string? Limit(string? value, int maximumLength)
        => value is { Length: > 0 }
            ? value[..Math.Min(value.Length, maximumLength)]
            : null;
}
