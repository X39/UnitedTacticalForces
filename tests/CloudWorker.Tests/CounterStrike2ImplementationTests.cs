using System.IO.Compression;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public sealed class CounterStrike2ImplementationTests
{
    [Fact]
    public void ConfigurationRenderer_EscapesLineAndQuoteInjection()
    {
        var implementation = new CounterStrike2ConfigurationImplementation();
        var hostname = implementation.Definitions.Single(q => q.Setting == "hostname");

        var rendered = implementation.Render(new Dictionary<string, JsonElement>
        {
            [hostname.Key] = JsonSerializer.SerializeToElement("UTF\"\nquit"),
        }).Single();
        var text = Encoding.UTF8.GetString(rendered.Content);

        Assert.Equal("hostname \"UTF\\\" quit\"\n", text);
        Assert.DoesNotContain("\nquit", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void Payload_AllowsRegisteredModes(int gameType, int gameMode)
    {
        var payload = Payload("de_dust2", gameType, gameMode);
        Assert.Equal((gameType, gameMode), payload.GetMode());
        Assert.Equal(("+map", "de_dust2"), payload.GetMap());
    }

    [Fact]
    public void Payload_RejectsUnregisteredMode()
        => Assert.Throws<InvalidDataException>(() => Payload("de_dust2", 6, 6).GetMode());

    [Fact]
    public void TrustedArchiveInstaller_RejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "utf-cs2-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archivePath = Path.Combine(root, "plugin.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../escape.dll");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("fixture");
            }
            Assert.Throws<InvalidDataException>(() => TrustedArchiveInstaller.Install(
                archivePath, Path.Combine(root, "install"), "cs2-plugin", new HashSet<string>()));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ExecutorRegistry_RejectsUnknownGameBeforeExecution()
    {
        var registry = new WorkerGameExecutorRegistry([]);
        Assert.Throws<InvalidOperationException>(() => registry.Create("unknown"));
    }

    private static CounterStrike2CommandPayload Payload(string map, int gameType, int gameMode)
        => new()
        {
            Command = new WorkerCommandPayload
            {
                Terrain = new WorkerSelectedContent
                {
                    Source = "BuiltIn", Identifier = map, DisplayName = map,
                },
                Scenario = new WorkerSelectedContent
                {
                    Source = "BuiltIn", Identifier = "mode", DisplayName = "mode",
                    Metadata = JsonSerializer.Serialize(new { gameType, gameMode }),
                },
            },
        };
}
