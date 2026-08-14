using X39.UnitedTacticalForces.Api.Middleware;
using Xunit;

namespace Api.Tests.Diagnostics;

public class UiVariantAttributionMiddlewareTests
{
    [Theory]
    [InlineData("classic", "classic")]
    [InlineData("CLASSIC", "classic")]
    [InlineData("react", "react")]
    [InlineData("React", "react")]
    public void ParseReturnsAllowListedVariant(string input, string expected)
        => Assert.Equal(expected, UiVariantAttributionMiddleware.Parse(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("next")]
    [InlineData("react, classic")]
    public void ParseIgnoresMissingOrInvalidVariant(string? input)
        => Assert.Null(UiVariantAttributionMiddleware.Parse(input));
}
