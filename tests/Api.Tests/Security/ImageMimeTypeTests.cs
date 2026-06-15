using X39.UnitedTacticalForces.Api.Helpers;
using Xunit;

namespace Api.Tests.Security;

public class ImageMimeTypeTests
{
    [Theory]
    [InlineData("image/png", "image/png")]
    [InlineData(" IMAGE/JPEG ", "image/jpeg")]
    [InlineData("image/webp; charset=binary", "image/webp")]
    public void Normalize_AllowsExpectedImageMimeTypes(string value, string expected)
    {
        Assert.Equal(expected, ImageMimeType.Normalize(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("text/css")]
    [InlineData("image/svg+xml")]
    [InlineData("image/png');background:url(https://example.invalid/a")]
    public void Normalize_RejectsUnsupportedOrCssBreakingMimeText(string value)
    {
        Assert.Null(ImageMimeType.Normalize(value));
    }

    [Fact]
    public void NormalizeOrDefault_FallsBackToPng()
    {
        Assert.Equal("image/png", ImageMimeType.NormalizeOrDefault("text/css"));
    }

    [Theory]
    [InlineData("89504E470D0A1A0A", "image/png")]
    [InlineData("FFD8FF", "image/jpeg")]
    [InlineData("474946383961", "image/gif")]
    [InlineData("424D", "image/bmp")]
    [InlineData("524946460000000057454250", "image/webp")]
    [InlineData("0000001C6674797061766966", "image/avif")]
    public void Detect_RecognizesSupportedImageSignatures(string hex, string expected)
    {
        Assert.Equal(expected, ImageMimeType.Detect(Convert.FromHexString(hex)));
    }

    [Fact]
    public void Detect_RejectsUnknownContent()
    {
        Assert.Null(ImageMimeType.Detect([0x3A, 0xE1, 0x7F, 0x95]));
    }
}
