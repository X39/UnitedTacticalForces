using Microsoft.Extensions.Configuration;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class HetznerRuntimeOptionsTests
{
    [Fact]
    public void ValidateConnection_AcceptsOfficialApiEndpoint()
    {
        var options = CreateOptions("https://api.hetzner.cloud/v1/");

        Assert.Null(options.ValidateConnection());
    }

    [Theory]
    [InlineData("https://api.hetzner.cloud.example/v1/")]
    [InlineData("https://api.hetzner.cloud@attacker.example/v1/")]
    [InlineData("https://127.0.0.1/v1/")]
    [InlineData("https://api.hetzner.cloud:8443/v1/")]
    public void ValidateConnection_RejectsNonOfficialEndpointByDefault(string baseUrl)
    {
        var options = CreateOptions(baseUrl);

        var failure = options.ValidateConnection();

        Assert.Contains("BaseUrl", failure, StringComparison.Ordinal);
        Assert.DoesNotContain(baseUrl, failure, StringComparison.Ordinal);
        Assert.DoesNotContain(options.Token, failure, StringComparison.Ordinal);
        var exception = Assert.Throws<InvalidOperationException>(() => options.GetBaseUri());
        Assert.Equal(failure, exception.Message);
    }

    [Fact]
    public void ValidateConnection_AllowsExplicitHttpsCustomEndpointForTesting()
    {
        var options = CreateOptions("https://unit.test/v1/");
        options.AllowCustomEndpointForTesting = true;

        Assert.Null(options.ValidateConnection());
    }

    [Fact]
    public void ValidateConnection_CustomEndpointSwitchDoesNotPermitPlainHttp()
    {
        var options = CreateOptions("http://unit.test/v1/");
        options.AllowCustomEndpointForTesting = true;

        Assert.Contains("absolute HTTPS URL", options.ValidateConnection(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationBinding_CannotEnableTestOnlyCustomEndpointSwitch()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{HetznerRuntimeOptions.SectionName}:Token"] = "test-token",
                [$"{HetznerRuntimeOptions.SectionName}:BaseUrl"] = "https://attacker.example/v1/",
                [$"{HetznerRuntimeOptions.SectionName}:AllowCustomEndpointForTesting"] = "true",
            })
            .Build();

        var options = new HetznerRuntimeOptionsProvider(configuration).Value;

        Assert.False(options.AllowCustomEndpointForTesting);
        Assert.Contains("official Hetzner API endpoint", options.ValidateConnection(), StringComparison.Ordinal);
    }

    private static HetznerRuntimeOptions CreateOptions(string baseUrl)
        => new()
        {
            Token = "test-token-that-must-not-appear-in-errors",
            BaseUrl = baseUrl,
        };
}
