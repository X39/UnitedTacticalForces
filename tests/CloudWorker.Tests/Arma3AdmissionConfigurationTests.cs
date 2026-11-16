using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Protects restored configuration from stale worker-owned Arma admission permissions.</summary>
public sealed class Arma3AdmissionConfigurationTests
{
    /// <summary>Replaces duplicate declarations while preserving settings, nested classes, and comments.</summary>
    [Fact]
    public void Reconcile_ReplacesStaleMultilineDeclarationsAndPreservesUnrelatedText()
    {
        const string source = """
            // headlessClients[] = {"comment-only"};
            hostname = "literal headlessClients[] = {}";
            class Nested { headlessClients[] = {"nested"}; };
            headlessClients /* owned */ [] = {
                "192.0.2.99", "192.0.2.98"
            };
            HEADLESSCLIENTS[] = {"192.0.2.97"};
            LocalCLIENT[] = {"192.0.2.99"};
            maxPlayers = 64;
            """;

        var result = Arma3AdmissionConfiguration.Reconcile(source, Topology("192.0.2.20"));

        Assert.Contains("// headlessClients[] = {\"comment-only\"};", result);
        Assert.Contains("hostname = \"literal headlessClients[] = {}\";", result);
        Assert.Contains("class Nested { headlessClients[] = {\"nested\"}; };", result);
        Assert.Contains("/* owned */", result);
        Assert.Contains("maxPlayers = 64;", result);
        Assert.Contains("headlessClients[] = {\"192.0.2.20\"};", result);
        Assert.Contains("localClient[] = {};", result);
        Assert.DoesNotContain("192.0.2.99", result);
        Assert.DoesNotContain("192.0.2.98", result);
        Assert.DoesNotContain("192.0.2.97", result);
        Assert.Equal(result, Arma3AdmissionConfiguration.Reconcile(result, Topology("192.0.2.20")));
    }

    /// <summary>Revokes old permissions when no headless workload remains in the command.</summary>
    [Fact]
    public void Reconcile_RemovedClientEmitsEmptyLists()
    {
        var result = Arma3AdmissionConfiguration.Reconcile(
            "headlessClients[] = {\"192.0.2.20\"}; localClient[] = {\"127.0.0.1\"};", []);

        Assert.Equal("headlessClients[] = {};" + Environment.NewLine
                     + "localClient[] = {};" + Environment.NewLine, result);
    }

    /// <summary>Uses loopback for colocated clients without granting remote endpoints local privileges.</summary>
    [Fact]
    public void Reconcile_ColocatedClientUsesLoopbackAndRemoteAddressesAreDeduplicated()
    {
        var topology = Topology("192.0.2.10", "192.0.2.20", "192.0.2.20");
        var result = Arma3AdmissionConfiguration.Reconcile(string.Empty, topology);

        Assert.Equal("headlessClients[] = {\"127.0.0.1\",\"192.0.2.20\"};" + Environment.NewLine
                     + "localClient[] = {\"127.0.0.1\"};" + Environment.NewLine, result);
    }

    /// <summary>Normalizes mapped addresses before colocated detection, deduplication, and rendering.</summary>
    [Fact]
    public void Reconcile_MappedAddressesMatchIpv4Primary()
    {
        var result = Arma3AdmissionConfiguration.Reconcile(
            string.Empty, Topology("::ffff:192.0.2.10", "::ffff:192.0.2.20", "192.0.2.20"));

        Assert.Equal("headlessClients[] = {\"127.0.0.1\",\"192.0.2.20\"};" + Environment.NewLine
                     + "localClient[] = {\"127.0.0.1\"};" + Environment.NewLine, result);
    }

    /// <summary>Refuses oversized restored files before replacing any user configuration.</summary>
    [Fact]
    public async Task ReconcileAdmissionAsync_RejectsOversizedRestoredFile()
    {
        using var directory = new TemporaryDirectory();
        var paths = new Arma3RuntimePaths(
            Path.Combine(directory.Path, "runtime"), Path.Combine(directory.Path, "state"),
            Guid.NewGuid(), "arma3server_x64");
        paths.EnsureDirectories();
        var source = new string(' ', 1024 * 1024 + 1);
        await File.WriteAllTextAsync(paths.ServerConfigurationPath, source);

        await Assert.ThrowsAsync<InvalidDataException>(() => new Arma3ConfigurationRenderer()
            .ReconcileAdmissionAsync(new Arma3CommandPayload(), paths, CancellationToken.None));

        Assert.Equal(source, await File.ReadAllTextAsync(paths.ServerConfigurationPath));
    }

    /// <summary>Blocks launch when configuration cannot be reconciled without ambiguous permissions.</summary>
    /// <param name="source">A malformed or preprocessed configuration.</param>
    [Theory]
    [InlineData("headlessClients[] += {\"192.0.2.1\"};")]
    [InlineData("headlessClients[] = {\"192.0.2.1\"}")]
    [InlineData("headlessClients = {\"192.0.2.1\"};")]
    [InlineData("headlessClients[] = {MACRO};")]
    [InlineData("#include \"other.cfg\"")]
    [InlineData("/* unfinished")]
    [InlineData("hostname = \"unfinished")]
    [InlineData("class Broken {")]
    public void Reconcile_RejectsAmbiguousConfiguration(string source)
        => Assert.Throws<InvalidDataException>(() => Arma3AdmissionConfiguration.Reconcile(source, Topology("192.0.2.20")));

    /// <summary>Rejects untrusted endpoint text before it can become executable configuration.</summary>
    /// <param name="address">An invalid address in the command topology.</param>
    [Theory]
    [InlineData("not-an-address")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("::ffff:0.0.0.0")]
    [InlineData("192.0.2.1\"}; password=\"bad")]
    public void Reconcile_RejectsInvalidEndpoint(string address)
        => Assert.Throws<InvalidDataException>(() => Arma3AdmissionConfiguration.Reconcile(string.Empty, Topology(address)));

    /// <summary>Ensures final reconciliation changes the restored file and leaves auxiliary files alone.</summary>
    [Fact]
    public async Task ReconcileAdmissionAsync_UpdatesRestoredPrimaryOnly()
    {
        using var directory = new TemporaryDirectory();
        var paths = new Arma3RuntimePaths(
            Path.Combine(directory.Path, "runtime"), Path.Combine(directory.Path, "state"),
            Guid.NewGuid(), "arma3server_x64");
        paths.EnsureDirectories();
        const string restored = "hostname = \"restored\"; headlessClients[] = {\"192.0.2.99\"};";
        await File.WriteAllTextAsync(paths.ServerConfigurationPath, restored);
        var renderer = new Arma3ConfigurationRenderer();
        await renderer.ReconcileAdmissionAsync(new Arma3CommandPayload
        {
            Command = new WorkerCommandPayload { IsPrimaryService = false, Topology = Topology("192.0.2.20") },
        }, paths, CancellationToken.None);
        Assert.Equal(restored, await File.ReadAllTextAsync(paths.ServerConfigurationPath));

        await renderer.ReconcileAdmissionAsync(new Arma3CommandPayload
        {
            Command = new WorkerCommandPayload { IsPrimaryService = true, Topology = Topology("192.0.2.20") },
        }, paths, CancellationToken.None);
        var actual = await File.ReadAllTextAsync(paths.ServerConfigurationPath);
        Assert.Contains("hostname = \"restored\";", actual);
        Assert.Contains("headlessClients[] = {\"192.0.2.20\"};", actual);
        Assert.DoesNotContain("192.0.2.99", actual);
    }

    /// <summary>Creates attributed endpoints for independent client address cases.</summary>
    /// <param name="addresses">Addresses assigned to the headless workload instances.</param>
    /// <returns>A topology containing one primary endpoint and the requested clients.</returns>
    private static WorkerNodeEndpoint[] Topology(params string[] addresses)
        => [new WorkerNodeEndpoint
            {
                ServiceIdentifier = "game-server", IsPrimary = true, Address = "192.0.2.10", Port = 2302,
            },
            .. addresses.Select((address, ordinal) => new WorkerNodeEndpoint
            {
                ServiceIdentifier = "headless-client", ServiceOrdinal = ordinal, Address = address,
            }),
        ];
}
