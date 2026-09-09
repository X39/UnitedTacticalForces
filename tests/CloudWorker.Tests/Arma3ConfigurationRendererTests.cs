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

    [Fact]
    public async Task RenderAsync_PrimaryNodeAllowsAttributedHeadlessClientAddresses()
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
                IsPrimaryService = true,
                Topology =
                [
                    new WorkerNodeEndpoint
                    {
                        ServiceIdentifier = "game-server",
                        Address = "192.0.2.10",
                        Port = 2302,
                        IsPrimary = true,
                    },
                    new WorkerNodeEndpoint
                    {
                        ServiceIdentifier = "headless-client",
                        Address = "192.0.2.20",
                    },
                ],
                Configuration = new Dictionary<string, JsonElement>
                {
                    ["server.cfg@hostname"] = JsonSerializer.SerializeToElement("UTF HC Test"),
                },
            },
        };

        await new Arma3ConfigurationRenderer().RenderAsync(payload, paths, CancellationToken.None);

        var serverConfig = await File.ReadAllTextAsync(paths.ServerConfigurationPath);
        Assert.Contains("headlessClients[] = {\"192.0.2.20\"};", serverConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("localClient", serverConfig, StringComparison.Ordinal);
    }

    /// <summary>Verifies typed values and the nested difficulty profile across all generated targets.</summary>
    [Fact]
    public async Task RenderAsync_WritesAllTargetsAndNestedDifficultyStructure()
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
                    ["server.cfg@verifySignatures"] = JsonSerializer.SerializeToElement(2),
                    ["basic.cfg@MaxBandwidth"] = JsonSerializer.SerializeToElement(100_000_000),
                    ["utfserver.arma3profile@thirdPersonView"] =
                        JsonSerializer.SerializeToElement(2),
                    ["utfserver.arma3profile@aiLevelPreset"] =
                        JsonSerializer.SerializeToElement(3),
                    ["utfserver.arma3profile@skillAI"] =
                        JsonSerializer.SerializeToElement(0.7m),
                    ["utfserver.arma3profile@precisionAI"] =
                        JsonSerializer.SerializeToElement(0.3m),
                    ["beserver.cfg@MaxPing"] = JsonSerializer.SerializeToElement(250),
                },
            },
        };

        await new Arma3ConfigurationRenderer().RenderAsync(payload, paths, CancellationToken.None);

        Assert.Contains(
            "verifySignatures = 2;",
            await File.ReadAllTextAsync(paths.ServerConfigurationPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "MaxBandwidth = 100000000;",
            await File.ReadAllTextAsync(paths.BasicConfigurationPath),
            StringComparison.Ordinal);
        var profilePath = Path.Combine(paths.ProfilePath, "UTFServer", "UTFServer.Arma3Profile");
        var profile = await File.ReadAllTextAsync(profilePath);
        Assert.Contains("class DifficultyPresets", profile, StringComparison.Ordinal);
        Assert.Contains("class CustomDifficulty", profile, StringComparison.Ordinal);
        Assert.Contains("class Options", profile, StringComparison.Ordinal);
        Assert.Contains("thirdPersonView = 2;", profile, StringComparison.Ordinal);
        Assert.Contains("aiLevelPreset = 3;", profile, StringComparison.Ordinal);
        Assert.Contains("skillAI = 0.7;", profile, StringComparison.Ordinal);
        Assert.Contains("precisionAI = 0.3;", profile, StringComparison.Ordinal);
        Assert.Equal(
            "MaxPing 250" + Environment.NewLine,
            await File.ReadAllTextAsync(paths.BattlEyeConfigurationPath));
    }
}
