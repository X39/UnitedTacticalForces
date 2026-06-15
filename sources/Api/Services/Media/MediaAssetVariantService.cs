using System.Collections.Concurrent;
using ImageMagick;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// ImageMagick-backed media variant cache restricted to raster web formats.
/// </summary>
[Singleton<MediaAssetVariantService, IMediaAssetVariantService>]
public sealed class MediaAssetVariantService : IMediaAssetVariantService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> VariantLocks = new(StringComparer.Ordinal);

    static MediaAssetVariantService()
    {
        ResourceLimits.Width            = 20_000;
        ResourceLimits.Height           = 20_000;
        ResourceLimits.ListLength       = 16;
        ResourceLimits.Thread           = 2;
        ResourceLimits.Time             = 30;
        ResourceLimits.Memory           = 256L * 1024L * 1024L;
        ResourceLimits.MaxMemoryRequest = 128L * 1024L * 1024L;
        ResourceLimits.Disk             = 512L * 1024L * 1024L;
    }

    /// <inheritdoc />
    public async Task<StoredMediaAssetFile> GetOrCreateAsync(
        StoredMediaAssetFile source,
        string targetContentType,
        CancellationToken cancellationToken = default)
    {
        var target = ResolveTarget(targetContentType);
        var variantDirectory = Path.Combine(Path.GetDirectoryName(source.FilePath)!, ".variants");
        var sourceId = Path.GetFileNameWithoutExtension(source.FilePath);
        var variantFileName = $"{sourceId}-{source.Sha256Hash[..16]}.{target.Extension}";
        var variantPath = Path.Combine(variantDirectory, variantFileName);
        if (File.Exists(variantPath))
            return new StoredMediaAssetFile(variantPath, target.ContentType, source.Sha256Hash);

        var variantLock = VariantLocks.GetOrAdd(variantPath, _ => new SemaphoreSlim(1, 1));
        await variantLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(variantPath))
            {
                Directory.CreateDirectory(variantDirectory);
                var temporaryPath = $"{variantPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    using var image = new MagickImage(
                        new FileInfo(source.FilePath),
                        ResolveSourceFormat(source.ContentType)
                    );
                    image.AutoOrient();
                    image.Strip();
                    image.Quality = target.Quality;
                    if (target.Format == MagickFormat.Jpeg)
                    {
                        image.BackgroundColor = MagickColors.White;
                        image.Alpha(AlphaOption.Remove);
                    }

                    await image.WriteAsync(temporaryPath, target.Format, cancellationToken).ConfigureAwait(false);
                    File.Move(temporaryPath, variantPath);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }

            return new StoredMediaAssetFile(variantPath, target.ContentType, source.Sha256Hash);
        }
        finally
        {
            variantLock.Release();
        }
    }

    private static VariantTarget ResolveTarget(string contentType)
        => contentType switch
        {
            "image/avif" => new VariantTarget("image/avif", "avif", MagickFormat.Avif, 82),
            "image/webp" => new VariantTarget("image/webp", "webp", MagickFormat.WebP, 85),
            "image/png"  => new VariantTarget("image/png", "png", MagickFormat.Png, 90),
            "image/jpeg" => new VariantTarget("image/jpeg", "jpg", MagickFormat.Jpeg, 88),
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported image variant type."),
        };

    private static MagickFormat ResolveSourceFormat(string contentType)
        => contentType switch
        {
            "image/avif" => MagickFormat.Avif,
            "image/bmp"  => MagickFormat.Bmp,
            "image/gif"  => MagickFormat.Gif,
            "image/jpeg" => MagickFormat.Jpeg,
            "image/png"  => MagickFormat.Png,
            "image/webp" => MagickFormat.WebP,
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported source image type."),
        };

    private sealed record VariantTarget(
        string ContentType,
        string Extension,
        MagickFormat Format,
        uint Quality);
}
