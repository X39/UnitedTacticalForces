using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Checks endpoint context recovery without depending on a running game process.</summary>
public sealed class Arma3LaunchTopologyContextTests
{
    /// <summary>Equivalent address spelling and endpoint order do not require a new launch.</summary>
    [Fact]
    public async Task MatchesAsync_NormalizesAddressesAndOrder()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var payload = CreatePayload();
        await Arma3LaunchTopologyContext.SaveAsync(payload, paths, CancellationToken.None);
        var reordered = payload with
        {
            Command = payload.Command with
            {
                Topology = payload.Command.Topology.Reverse()
                    .Select(endpoint => endpoint.IsPrimary
                        ? endpoint with { Address = "2001:0db8:0000:0000:0000:0000:0000:0001" }
                        : endpoint)
                    .ToArray(),
            },
        };

        Assert.True(await Arma3LaunchTopologyContext.MatchesAsync(reordered, paths, CancellationToken.None));
    }

    /// <summary>Changed addresses cannot reuse the applied topology of a previous launch.</summary>
    [Fact]
    public async Task MatchesAsync_RejectsChangedAddress()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var payload = CreatePayload();
        await Arma3LaunchTopologyContext.SaveAsync(payload, paths, CancellationToken.None);
        var changed = payload with
        {
            Command = payload.Command with
            {
                Topology = payload.Command.Topology
                    .Select(endpoint => endpoint.IsPrimary ? endpoint with { Address = "2001:db8::2" } : endpoint)
                    .ToArray(),
            },
        };

        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(changed, paths, CancellationToken.None));
        await Arma3LaunchTopologyContext.SaveAsync(changed, paths, CancellationToken.None);
        Assert.True(await Arma3LaunchTopologyContext.MatchesAsync(changed, paths, CancellationToken.None));
        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(payload, paths, CancellationToken.None));
    }

    /// <summary>Attribution, workload role, and ports are part of the applied context.</summary>
    /// <param name="changedField">The identity component changed after the original launch.</param>
    [Theory]
    [InlineData("server")]
    [InlineData("deployment")]
    [InlineData("service")]
    [InlineData("ordinal")]
    [InlineData("primary")]
    [InlineData("port")]
    [InlineData("endpoint-port")]
    public async Task MatchesAsync_RejectsChangedAttribution(string changedField)
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var payload = CreatePayload();
        await Arma3LaunchTopologyContext.SaveAsync(payload, paths, CancellationToken.None);
        var command = changedField switch
        {
            "server" => payload.Command with { ServerId = Guid.NewGuid() },
            "deployment" => payload.Command with { DeploymentId = Guid.NewGuid() },
            "service" => payload.Command with { ServiceIdentifier = "headless-client" },
            "ordinal" => payload.Command with { ServiceOrdinal = 1 },
            "primary" => payload.Command with { IsPrimaryService = false },
            "port" => payload.Command with { Port = 2303 },
            "endpoint-port" => payload.Command with
            {
                Topology = payload.Command.Topology
                    .Select(endpoint => endpoint.IsPrimary ? endpoint with { Port = 2303 } : endpoint)
                    .ToArray(),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField)),
        };

        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(
            payload with { Command = command }, paths, CancellationToken.None));
    }

    /// <summary>Only legacy commands without topology may recover when no context was recorded.</summary>
    [Fact]
    public async Task MatchesAsync_MissingStateAllowsOnlyLegacyTopology()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var payload = CreatePayload();
        var legacy = payload with { Command = payload.Command with { Topology = [] } };

        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(payload, paths, CancellationToken.None));
        Assert.True(await Arma3LaunchTopologyContext.MatchesAsync(legacy, paths, CancellationToken.None));
        await Arma3LaunchTopologyContext.SaveAsync(payload, paths, CancellationToken.None);
        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(legacy, paths, CancellationToken.None));
    }

    /// <summary>Malformed and inaccessible persisted context cannot authorize process adoption.</summary>
    [Fact]
    public async Task MatchesAsync_InvalidOrUnreadableStateFailsClosed()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var payload = CreatePayload();
        Directory.CreateDirectory(paths.AgentStatePath);
        var file = Path.Combine(paths.AgentStatePath, Arma3LaunchTopologyContext.FileName);
        await File.WriteAllTextAsync(file, "invalid state");
        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(payload, paths, CancellationToken.None));
        await File.WriteAllTextAsync(file, new string('z', 64));
        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(payload, paths, CancellationToken.None));
        File.Delete(file);
        Directory.CreateDirectory(file);
        Assert.False(await Arma3LaunchTopologyContext.MatchesAsync(payload, paths, CancellationToken.None));
    }

    /// <summary>State stores only a digest and ignores secret configuration when comparing topology.</summary>
    [Fact]
    public async Task SaveAsync_ExcludesConfigurationAndSecrets()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var payload = CreatePayload();
        var withSecret = payload with
        {
            Command = payload.Command with
            {
                Configuration = new Dictionary<string, JsonElement>
                {
                    ["server.cfg@password"] = JsonSerializer.SerializeToElement("private-password"),
                },
            },
        };
        await Arma3LaunchTopologyContext.SaveAsync(withSecret, paths, CancellationToken.None);

        var text = await File.ReadAllTextAsync(Path.Combine(paths.AgentStatePath, Arma3LaunchTopologyContext.FileName));
        Assert.Matches("^[0-9A-F]{64}$", text);
        Assert.DoesNotContain("private-password", text, StringComparison.Ordinal);
        Assert.True(await Arma3LaunchTopologyContext.MatchesAsync(payload, paths, CancellationToken.None));
        Assert.Single(Directory.GetFiles(paths.AgentStatePath));
    }

    /// <summary>Cancellation remains observable even when the compatibility path has no state file.</summary>
    [Fact]
    public async Task MatchesAsync_PropagatesCancellation()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Arma3LaunchTopologyContext.MatchesAsync(CreatePayload(), CreatePaths(directory.Path), cancellation.Token));
    }

    /// <summary>Creates separate runtime and agent-state paths for a test deployment.</summary>
    /// <param name="root">The disposable directory owning the test files.</param>
    /// <returns>Paths that keep launch state outside restored game data.</returns>
    private static Arma3RuntimePaths CreatePaths(string root)
        => new(Path.Combine(root, "runtime"), Path.Combine(root, "state"), Guid.NewGuid(), "arma3server_x64");

    /// <summary>Creates a primary and auxiliary topology using literal provider addresses.</summary>
    /// <returns>An attributed command whose endpoint identity can be varied by each test.</returns>
    private static Arma3CommandPayload CreatePayload()
        => new()
        {
            Command = new WorkerCommandPayload
            {
                ServerId = Guid.NewGuid(),
                DeploymentId = Guid.NewGuid(),
                GameIdentifier = "arma3",
                Topology =
                [
                    new WorkerNodeEndpoint
                    {
                        ServiceIdentifier = "game-server",
                        Address = "2001:db8::1",
                        Port = 2302,
                        IsPrimary = true,
                    },
                    new WorkerNodeEndpoint
                    {
                        ServiceIdentifier = "headless-client",
                        Address = "192.0.2.20",
                    },
                ],
            },
        };
}
