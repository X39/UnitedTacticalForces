using Microsoft.Net.Http.Headers;

namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Resolves explicit image formats and HTTP content negotiation for media assets.
/// </summary>
public static class MediaAssetContentType
{
    private static readonly string[] NegotiatedContentTypes =
    [
        "image/avif",
        "image/webp",
        "image/png",
        "image/jpeg",
    ];

    /// <summary>
    /// Resolves a requested format. Explicit query values take precedence over the Accept header.
    /// </summary>
    public static MediaAssetContentTypeResolution Resolve(
        string? format,
        string sourceContentType,
        IEnumerable<MediaTypeHeaderValue> acceptedMediaTypes)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            var explicitContentType = format.Trim().ToLowerInvariant() switch
            {
                "original"    => sourceContentType,
                "avif"        => "image/avif",
                "webp"        => "image/webp",
                "png"         => "image/png",
                "jpg"         => "image/jpeg",
                "jpeg"        => "image/jpeg",
                "image/avif"  => "image/avif",
                "image/webp"  => "image/webp",
                "image/png"   => "image/png",
                "image/jpeg"  => "image/jpeg",
                _             => null,
            };
            return explicitContentType is null
                ? new MediaAssetContentTypeResolution(null, true)
                : new MediaAssetContentTypeResolution(explicitContentType, false);
        }

        var accepted = acceptedMediaTypes
            .Where(value => value.Quality is null or > 0)
            .OrderByDescending(value => value.Quality ?? 1)
            .ToArray();
        foreach (var acceptedMediaType in accepted)
        {
            var contentType = NegotiatedContentTypes.FirstOrDefault(value =>
                string.Equals(value, acceptedMediaType.MediaType.Value, StringComparison.OrdinalIgnoreCase)
            );
            if (contentType is not null)
                return new MediaAssetContentTypeResolution(contentType, false);
        }

        return new MediaAssetContentTypeResolution(sourceContentType, false);
    }
}

/// <summary>
/// Result of resolving a requested media asset representation.
/// </summary>
public readonly record struct MediaAssetContentTypeResolution(string? ContentType, bool IsInvalid);
