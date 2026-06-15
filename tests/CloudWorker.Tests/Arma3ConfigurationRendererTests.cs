using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

public class Arma3ConfigurationRendererTests
{
    [Fact]
    public async Task RenderAsync_WritesAllowlistedConfigWithoutArbitraryPaths()
    {
        using var directory = new TemporaryDirectory();
        var paths = new Arma3RuntimePaths(
            Path.Combine(directory.Path, "runtime"),
            Path.Combine(directory.Path, "state"),
            Guid.NewGuid(),
            "arma3server_x64");
        var payload = new Arma3CommandPayload
        {
            ServerConfiguration = new Dictionary<string, JsonElement>
            {
                ["hostname"] = JsonSerializer.SerializeToElement("UTF Test"),
                ["maxPlayers"] = JsonSerializer.SerializeToElement(64),
            },
            BattlEyeConfiguration = new Dictionary<string, string>
            {
                ["RConPort"] = "2307",
            },
        };

        await new Arma3ConfigurationRenderer().RenderAsync(payload, paths, CancellationToken.None);

        var serverConfig = await File.ReadAllTextAsync(paths.ServerConfigurationPath);
        Assert.Contains("hostname = \"UTF Test\";", serverConfig, StringComparison.Ordinal);
        Assert.Contains("maxPlayers = 64;", serverConfig, StringComparison.Ordinal);
        Assert.Equal("RConPort 2307" + Environment.NewLine, await File.ReadAllTextAsync(paths.BattlEyeConfigurationPath));
    }

    [Fact]
    public async Task RenderAsync_RejectsConfigKeyInjection()
    {
        using var directory = new TemporaryDirectory();
        var paths = new Arma3RuntimePaths(
            Path.Combine(directory.Path, "runtime"),
            Path.Combine(directory.Path, "state"),
            Guid.NewGuid(),
            "arma3server_x64");
        var payload = new Arma3CommandPayload
        {
            ServerConfiguration = new Dictionary<string, JsonElement>
            {
                ["hostname; password"] = JsonSerializer.SerializeToElement("bad"),
            },
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new Arma3ConfigurationRenderer().RenderAsync(payload, paths, CancellationToken.None));
    }
}
