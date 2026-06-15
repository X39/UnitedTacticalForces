using Microsoft.Net.Http.Headers;
using X39.UnitedTacticalForces.Api.Services.Media;
using Xunit;

namespace Api.Tests.Media;

public class MediaAssetContentTypeTests
{
    [Theory]
    [InlineData("original", "image/png")]
    [InlineData("avif", "image/avif")]
    [InlineData("webp", "image/webp")]
    [InlineData("png", "image/png")]
    [InlineData("jpg", "image/jpeg")]
    public void Resolve_MapsExplicitFormats(string format, string expected)
    {
        var result = MediaAssetContentType.Resolve(format, "image/png", []);

        Assert.False(result.IsInvalid);
        Assert.Equal(expected, result.ContentType);
    }

    [Fact]
    public void Resolve_UsesPreferredConcreteAcceptType()
    {
        var result = MediaAssetContentType.Resolve(
            null,
            "image/jpeg",
            MediaTypeHeaderValue.ParseList(["image/avif,image/webp;q=0.9,*/*;q=0.8"])
        );

        Assert.False(result.IsInvalid);
        Assert.Equal("image/avif", result.ContentType);
    }

    [Fact]
    public void Resolve_PreservesSourceForWildcardAccept()
    {
        var result = MediaAssetContentType.Resolve(
            null,
            "image/gif",
            MediaTypeHeaderValue.ParseList(["image/*,*/*;q=0.8"])
        );

        Assert.False(result.IsInvalid);
        Assert.Equal("image/gif", result.ContentType);
    }

    [Fact]
    public void Resolve_RespectsAcceptQualityBeforeServerPreference()
    {
        var result = MediaAssetContentType.Resolve(
            null,
            "image/png",
            MediaTypeHeaderValue.ParseList(["image/avif;q=0.5,image/webp;q=0.9"])
        );

        Assert.False(result.IsInvalid);
        Assert.Equal("image/webp", result.ContentType);
    }

    [Fact]
    public void Resolve_RejectsUnknownExplicitFormat()
    {
        var result = MediaAssetContentType.Resolve("svg", "image/png", []);

        Assert.True(result.IsInvalid);
        Assert.Null(result.ContentType);
    }
}
