using System.Security.Cryptography;
using ImageMagick;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.Media;
using Xunit;

namespace Api.Tests.Media;

public sealed class MediaAssetVariantServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"utf-media-variant-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("image/avif")]
    [InlineData("image/webp")]
    [InlineData("image/jpeg")]
    public async Task GetOrCreateAsync_ConvertsAndCachesWebVariants(string targetContentType)
    {
        Directory.CreateDirectory(_directory);
        var sourcePath = Path.Combine(_directory, "asset.png");
        using (var image = new MagickImage(MagickColors.Red, 1, 1))
            await image.WriteAsync(sourcePath, MagickFormat.Png);
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        var source = new StoredMediaAssetFile(sourcePath, "image/png", hash);
        var service = new MediaAssetVariantService();

        var first = await service.GetOrCreateAsync(source, targetContentType);
        var second = await service.GetOrCreateAsync(source, targetContentType);

        Assert.Equal(first, second);
        Assert.Equal(targetContentType, first.ContentType);
        Assert.True(File.Exists(first.FilePath));
        Assert.Equal(targetContentType, ImageMimeType.Detect(await File.ReadAllBytesAsync(first.FilePath)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
