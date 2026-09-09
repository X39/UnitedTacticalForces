using Microsoft.Extensions.Configuration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using Xunit;

namespace Api.Tests.ModPacks;

public sealed class GitModSourceProviderTests
{
    [Fact]
    public async Task ResolveAsync_CanonicalizesAllowlistedHttpsRepository()
    {
        var provider = CreateProvider("github.com");

        var result = await provider.ResolveAsync("https://github.com/example/mod.git?token=discarded#fragment", "main");

        Assert.Equal("https://github.com/example/mod.git", result.CanonicalSourceIdentifier);
        Assert.Equal("main", result.VersionSelector);
    }

    [Theory]
    [InlineData("http://github.com/example/mod.git")]
    [InlineData("https://user:secret@github.com/example/mod.git")]
    [InlineData("https://127.0.0.1/example/mod.git")]
    [InlineData("https://untrusted.test/example/mod.git")]
    public async Task ResolveAsync_RejectsUnsafeSources(string source)
    {
        var provider = CreateProvider("github.com");

        await Assert.ThrowsAsync<ArgumentException>(() => provider.ResolveAsync(source, null));
    }

    private static GitModSourceProvider CreateProvider(params string[] hosts)
    {
        var values = hosts.Select((host, index) => new KeyValuePair<string, string?>(
            $"Hosting:ModSources:Git:AllowedHosts:{index}", host));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new GitModSourceProvider(configuration);
    }
}
