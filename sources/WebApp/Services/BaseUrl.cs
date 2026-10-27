using System.Diagnostics.CodeAnalysis;

namespace X39.UnitedTacticalForces.WebApp.Services;

/// <summary>Resolves public client resources and API endpoints against their configured origins.</summary>
/// <param name="ApiUrl">The absolute or client-relative API base URL.</param>
/// <param name="SelfUrl">The absolute base URL of the current client.</param>
public record BaseUrl(string ApiUrl, string SelfUrl)
{
    /// <summary>Gets the normalized API base URI, resolving relative configuration against the client.</summary>
    /// <value>The absolute API URI with a trailing slash.</value>
    public Uri ApiUri => ResolveBaseUri(ApiUrl, SelfUri);
    /// <summary>Gets the normalized client base URI.</summary>
    /// <value>The absolute client URI with a trailing slash.</value>
    public Uri SelfUri => ResolveBaseUri(SelfUrl, fallbackBaseUri: null);
    /// <summary>Resolves an API endpoint to its public URL.</summary>
    /// <param name="path">The endpoint path relative to the API base.</param>
    /// <returns>The absolute API endpoint URL.</returns>
    public string ResolveApiUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path) => ResolveApiUri(path).ToString();

    /// <summary>Builds an authentication URL with a return location trusted by the API.</summary>
    /// <param name="path">The authentication endpoint relative to the API base.</param>
    /// <param name="returnPath">The destination relative to the current client base.</param>
    /// <returns>The endpoint URL with a local return path for a same-origin API.</returns>
    public string ResolveApiUrlWithReturnUrl(
        [StringSyntax(StringSyntaxAttribute.Uri)]string path,
        [StringSyntax(StringSyntaxAttribute.Uri)]string returnPath)
    {
        var separator = path.Contains("?", StringComparison.Ordinal) ? "&" : "?";
        var returnUri = ResolveSelfUri(returnPath);
        var sameOrigin = string.Equals(ApiUri.GetLeftPart(UriPartial.Authority),
            SelfUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
        var encodedReturnUrl = Uri.EscapeDataString(sameOrigin ? returnUri.PathAndQuery + returnUri.Fragment
            : returnUri.ToString());
        return ResolveApiUrl($"{path}{separator}returnUrl={encodedReturnUrl}");
    }

    /// <summary>Resolves an API endpoint against the normalized API base.</summary>
    /// <param name="path">The relative API endpoint path.</param>
    /// <returns>The absolute endpoint URI.</returns>
    public Uri ResolveApiUri([StringSyntax(StringSyntaxAttribute.Uri)]string path)
    {
        path = path.TrimStart('/');
        return new Uri(ApiUri, path);
    }

    /// <summary>Resolves a client route to its public URL.</summary>
    /// <param name="path">The relative client route.</param>
    /// <returns>The absolute client URL.</returns>
    public string ResolveSelfUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path) => ResolveSelfUri(path).ToString();

    /// <summary>Resolves a client route against the normalized client base.</summary>
    /// <param name="path">The relative client route.</param>
    /// <returns>The absolute client URI.</returns>
    public Uri ResolveSelfUri([StringSyntax(StringSyntaxAttribute.Uri)]string path)
    {
        path = path.TrimStart('/');
        return new Uri(SelfUri, path);
    }

    /// <summary>Resolves a static resource served by the client host.</summary>
    /// <param name="path">The relative resource path.</param>
    /// <returns>The absolute resource URL.</returns>
    public string ResolveResourceUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path) => ResolveSelfUrl(path);

    /// <summary>Resolves uploaded API media or a client static image without changing absolute URLs.</summary>
    /// <param name="path">An absolute image URL or relative image path.</param>
    /// <returns>The public image URL.</returns>
    public string ResolveImageUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        var normalizedPath = path.TrimStart('/');
        return normalizedPath.StartsWith("media-assets/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith("field-reports/media/", StringComparison.OrdinalIgnoreCase)
            ? ResolveApiUrl(normalizedPath)
            : ResolveSelfUrl(normalizedPath);
    }

    /// <summary>Normalizes a configured base URL, resolving relative values when a fallback exists.</summary>
    /// <param name="configuredUrl">The configured absolute or relative URL.</param>
    /// <param name="fallbackBaseUri">The base used for empty or relative configuration.</param>
    /// <returns>An absolute base URI with a trailing slash.</returns>
    /// <exception cref="InvalidOperationException">No absolute base is available for the configuration.</exception>
    /// <exception cref="UriFormatException">The configured URL cannot be resolved.</exception>
    private static Uri ResolveBaseUri(string configuredUrl, Uri? fallbackBaseUri)
    {
        var trimmedUrl = configuredUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl))
        {
            if (fallbackBaseUri is not null)
                return EnsureTrailingSlash(fallbackBaseUri);

            throw new InvalidOperationException("A self base URL must be configured.");
        }

        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
            return EnsureTrailingSlash(absoluteUri);

        if (fallbackBaseUri is null)
            throw new InvalidOperationException("A relative base URL requires an absolute fallback URL.");

        return EnsureTrailingSlash(new Uri(fallbackBaseUri, trimmedUrl));
    }

    /// <summary>Preserves directory semantics when resolving paths against a base URI.</summary>
    /// <param name="uri">The absolute base URI.</param>
    /// <returns>The base URI ending in a slash.</returns>
    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{value}/", UriKind.Absolute);
    }
}
