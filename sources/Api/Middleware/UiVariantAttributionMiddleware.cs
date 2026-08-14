using System.Diagnostics;

namespace X39.UnitedTacticalForces.Api.Middleware;

/// <summary>
/// Attributes requests to one of the explicitly supported UI variants for aggregate diagnostics.
/// The value is observational only and never participates in authorization or application behavior.
/// </summary>
internal sealed class UiVariantAttributionMiddleware(
    RequestDelegate next,
    ILogger<UiVariantAttributionMiddleware> logger)
{
    internal const string HeaderName = "X-UTF-UI-Variant";
    internal const string ActivityTagName = "utf.ui.variant";

    public async Task InvokeAsync(HttpContext context)
    {
        var variant = Parse(context.Request.Headers[HeaderName]);
        if (variant is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        Activity.Current?.SetTag(ActivityTagName, variant);
        using (logger.BeginScope(new Dictionary<string, object> { ["UtfUiVariant"] = variant, }))
            await next(context).ConfigureAwait(false);
    }

    internal static string? Parse(string? value)
    {
        if (string.Equals(value, "classic", StringComparison.OrdinalIgnoreCase))
            return "classic";
        return string.Equals(value, "react", StringComparison.OrdinalIgnoreCase)
            ? "react"
            : null;
    }
}

internal static class UiVariantAttributionMiddlewareExtensions
{
    internal static IApplicationBuilder UseUiVariantAttribution(this IApplicationBuilder app)
        => app.UseMiddleware<UiVariantAttributionMiddleware>();
}
