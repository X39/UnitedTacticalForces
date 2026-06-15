using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HostingFailureSanitizerTests
{
    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("token: abc.def.ghi")]
    [InlineData("Authorization Bearer-secret-value")]
    [InlineData("https://worker:credential@example.invalid/path")]
    public void Sanitize_RemovesCredentialShapedValues(string value)
    {
        var result = HostingFailureSanitizer.Sanitize(value);

        Assert.DoesNotContain("hunter2", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc.def.ghi", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer-secret-value", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential@example", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_BoundsPersistedFailureText()
    {
        var result = HostingFailureSanitizer.Sanitize(new string('x', 5000));

        Assert.Equal(4096, result.Length);
    }
}
