using System.Diagnostics.CodeAnalysis;

namespace X39.UnitedTacticalForces.WebApp.Services;

public record BaseUrl(string ApiUrl, string SelfUrl)
{
    public Uri ApiUri => ResolveBaseUri(ApiUrl, SelfUri);
    public Uri SelfUri => ResolveBaseUri(SelfUrl, fallbackBaseUri: null);
    public string ResolveApiUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path) => ResolveApiUri(path).ToString();

    public string ResolveApiUrlWithReturnUrl(
        [StringSyntax(StringSyntaxAttribute.Uri)]string path,
        [StringSyntax(StringSyntaxAttribute.Uri)]string returnPath)
    {
        var separator = path.Contains("?", StringComparison.Ordinal) ? "&" : "?";
        var encodedReturnUrl = Uri.EscapeDataString(ResolveSelfUrl(returnPath));
        return ResolveApiUrl($"{path}{separator}returnUrl={encodedReturnUrl}");
    }

    public Uri ResolveApiUri([StringSyntax(StringSyntaxAttribute.Uri)]string path)
    {
        path = path.TrimStart('/');
        return new Uri(ApiUri, path);
    }

    public string ResolveSelfUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path) => ResolveSelfUri(path).ToString();

    public Uri ResolveSelfUri([StringSyntax(StringSyntaxAttribute.Uri)]string path)
    {
        path = path.TrimStart('/');
        return new Uri(SelfUri, path);
    }

    public string ResolveResourceUrl([StringSyntax(StringSyntaxAttribute.Uri)]string path) => ResolveSelfUrl(path);

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

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{value}/", UriKind.Absolute);
    }
}
