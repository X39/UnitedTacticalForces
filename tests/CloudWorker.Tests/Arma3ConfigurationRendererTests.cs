using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
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
            Command = new WorkerCommandPayload
            {
                GameIdentifier = Arma3ConfigurationImplementation.Identifier,
                Configuration = new Dictionary<string, JsonElement>
                {
                    ["server.cfg@hostname"] = JsonSerializer.SerializeToElement("UTF Test"),
                    ["server.cfg@maxPlayers"] = JsonSerializer.SerializeToElement(64),
                    ["beserver.cfg@RConPort"] = JsonSerializer.SerializeToElement(2307),
                },
            },
        };

        await new Arma3ConfigurationRenderer().RenderAsync(payload, paths, CancellationToken.None);

        var serverConfig = await File.ReadAllTextAsync(paths.ServerConfigurationPath);
        Assert.Contains("hostname = \"UTF Test\";", serverConfig, StringComparison.Ordinal);
        Assert.Contains("maxPlayers = 64;", serverConfig, StringComparison.Ordinal);
        Assert.Equal("RConPort 2307" + Environment.NewLine, await File.ReadAllTextAsync(paths.BattlEyeConfigurationPath));
        var expected = GameConfigurationImplementations.Get(Arma3ConfigurationImplementation.Identifier)
            .Render(payload.Command.Configuration)
            .Single(q => q.Path == Arma3ConfigurationImplementation.ServerTarget)
            .Content;
        Assert.Equal(expected, await File.ReadAllBytesAsync(paths.ServerConfigurationPath));
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
            Command = new WorkerCommandPayload
            {
                GameIdentifier = Arma3ConfigurationImplementation.Identifier,
                Configuration = new Dictionary<string, JsonElement>
                {
                    ["server.cfg@hostname; password"] = JsonSerializer.SerializeToElement("bad"),
                },
            },
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new Arma3ConfigurationRenderer().RenderAsync(payload, paths, CancellationToken.None));
    }
}
