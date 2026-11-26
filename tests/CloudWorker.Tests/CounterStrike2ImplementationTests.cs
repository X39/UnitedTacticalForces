using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
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
                archivePath, Path.Combine(root, "install"), "cs2", "cs2-plugin", new HashSet<string>()));
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

    /// <summary>Verifies that a started server's human players come from the RCON status on the start port.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_AfterStart_ReadsHumansThroughRconStatus()
    {
        using var directory = new TemporaryDirectory();
        var rcon = new FakeRconClient("hostname: UTF\nplayers : 2 humans, 1 bots (16 max) (not hibernating)\n");
        var executor = await StartedExecutorAsync(directory, rcon, 27020);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(WorkerPlayerObservation.Players(2, 16), observation);
        Assert.Equal(27020, rcon.LastPort);
        Assert.Equal(StartPassword, rcon.LastPassword);
        Assert.Equal(TimeSpan.FromSeconds(2), rcon.LastTimeout);
    }

    /// <summary>Verifies that a stopped process is reported as measuring without any query.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_NoRunningProcess_ReportsMeasuringWithoutQuerying()
    {
        using var directory = new TemporaryDirectory();
        var rcon = new FakeRconClient("players : 2 humans");
        var connection = Connection();
        IWorkerCommandExecutor executor = new CounterStrike2WorkerCommandExecutor(
            connection, Options(directory.Path), process: new FakeProcessController(connection, directory.Path), rcon: rcon);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, observation.UnavailableReason);
        Assert.Equal(0, rcon.RequestCount);
    }

    /// <summary>Verifies that an unreadable status reply never yields a number and never leaks the secret.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_UnparseableReply_ReportsQueryFailedWithoutLeakingSecrets()
    {
        using var directory = new TemporaryDirectory();
        const string body = "secret-reply-body-without-a-players-line";
        var executor = await StartedExecutorAsync(directory, new FakeRconClient(body), 27015);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Null(observation.Count);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, observation.UnavailableReason);
        Assert.DoesNotContain(StartPassword, observation.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(body, observation.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Verifies that the RCON password is never persisted in the worker state directory.</summary>
    /// <returns>A task that completes after the check.</returns>
    [Fact]
    public async Task Start_DoesNotPersistTheRconPasswordInWorkerState()
    {
        using var directory = new TemporaryDirectory();
        _ = await StartedExecutorAsync(directory, new FakeRconClient("players : 1 humans"), 27015);

        var stateDirectory = Path.Combine(directory.Path, "agent");
        var leaked = Directory.Exists(stateDirectory)
                     && Directory.EnumerateFiles(stateDirectory, "*", SearchOption.AllDirectories)
                         .Any(file => File.ReadAllText(file).Contains(StartPassword, StringComparison.Ordinal));
        Assert.False(leaked);
    }

    /// <summary>Verifies the status request against a loopback RCON server that answers in two packets.</summary>
    /// <returns>A task that completes after the request.</returns>
    [Fact]
    public async Task RconClient_RequestStatus_JoinsResponsePackets()
    {
        using var server = new LoopbackRconServer("pw", ["players : 4 humans, ", "0 bots (10 max)\n"]);

        var text = await new CounterStrike2RconClient().RequestStatusAsync(
            server.Port, "pw", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal("players : 4 humans, 0 bots (10 max)\n", text);
        Assert.Equal("status", await server.CommandTask);
    }

    /// <summary>Verifies that a wrong password and a closed port both end in null, never an exception.</summary>
    /// <returns>A task that completes after both requests.</returns>
    [Fact]
    public async Task RconClient_RequestStatus_WrongPasswordOrClosedPort_ReturnsNull()
    {
        using var server = new LoopbackRconServer("pw", ["players : 4 humans"]);
        var client = new CounterStrike2RconClient();

        var wrong = await client.RequestStatusAsync(server.Port, "nope", TimeSpan.FromSeconds(5), CancellationToken.None);
        var closedPort = server.Port;
        server.Dispose();
        var gone = await client.RequestStatusAsync(closedPort, "pw", TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Null(wrong);
        Assert.Null(gone);
    }

    /// <summary>Verifies that without a held password (as after a worker restart) A2S_INFO on the persisted port answers.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_NoPasswordHeld_FallsBackToA2sOnThePersistedPort()
    {
        using var directory = new TemporaryDirectory();
        var rcon = new FakeRconClient("players : 9 humans");
        var a2s = new FakeA2sInfoClient(new A2sInfo(6, 10, 2));
        var connection = Connection();
        IWorkerCommandExecutor executor = new CounterStrike2WorkerCommandExecutor(
            connection,
            Options(directory.Path),
            process: new FakeProcessController(connection, directory.Path, running: true, gamePort: 27020),
            rcon: rcon,
            a2s: a2s);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(WorkerPlayerObservation.Players(4, 10), observation);
        Assert.Equal(27020, a2s.LastPort);
        Assert.Equal(TimeSpan.FromSeconds(2), a2s.LastTimeout);
        Assert.Equal(0, rcon.RequestCount);
    }

    /// <summary>Verifies that a status reply without a players line falls back to A2S_INFO.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_StatusWithoutPlayersLine_FallsBackToA2s()
    {
        using var directory = new TemporaryDirectory();
        var a2s = new FakeA2sInfoClient(new A2sInfo(3, 12, 0));
        var executor = await StartedExecutorAsync(directory, new FakeRconClient("hostname: UTF"), 27030, a2s);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(WorkerPlayerObservation.Players(3, 12), observation);
        Assert.Equal(27030, a2s.LastPort);
    }

    /// <summary>Verifies that RCON and A2S both failing report query-failed, never a guessed count.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_RconAndA2sFail_ReportsQueryFailed()
    {
        using var directory = new TemporaryDirectory();
        var a2s = new FakeA2sInfoClient(null);
        var executor = await StartedExecutorAsync(directory, new FakeRconClient(null), 27015, a2s);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Null(observation.Count);
        Assert.Null(observation.Capacity);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, observation.UnavailableReason);
        Assert.Equal(1, a2s.QueryCount);
    }

    /// <summary>Verifies that an identity persisted without a game port falls back to the default port 27015.</summary>
    /// <returns>A task that completes after the observation.</returns>
    [Fact]
    public async Task ObservePlayers_IdentityWithoutGamePort_QueriesDefaultPort()
    {
        using var directory = new TemporaryDirectory();
        var a2s = new FakeA2sInfoClient(new A2sInfo(1, 2, 0));
        var connection = Connection();
        IWorkerCommandExecutor executor = new CounterStrike2WorkerCommandExecutor(
            connection,
            Options(directory.Path),
            process: new FakeProcessController(connection, directory.Path, running: true, gamePort: null),
            rcon: new FakeRconClient(null),
            a2s: a2s);

        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(WorkerPlayerObservation.Players(1, 2), observation);
        Assert.Equal(27015, a2s.LastPort);
    }

    /// <summary>Verifies that a terminated server forgets the RCON password.</summary>
    /// <returns>A task that completes after the observations.</returns>
    [Fact]
    public async Task ObservePlayers_AfterTerminate_ReportsMeasuringAndHoldsNoPassword()
    {
        using var directory = new TemporaryDirectory();
        var rcon = new FakeRconClient("players : 5 humans (8 max)");
        var executor = await StartedExecutorAsync(directory, rcon, 27015);

        await executor.CompensateSupersededAsync(StartCommand(Connection(), 27015), CancellationToken.None);
        var observation = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, observation.UnavailableReason);
        Assert.Equal(0, rcon.RequestCount);
    }

    /// <summary>Verifies that process identities with and without a game port deserialize as expected.</summary>
    [Fact]
    public void ProcessIdentity_GamePort_RoundTripsAndOlderIdentitiesReadAsNull()
    {
        var identity = new CounterStrike2ProcessIdentity(
            Guid.NewGuid(), Guid.NewGuid(), 7, DateTimeOffset.UtcNow, "/srv/cs2", 27020);
        var json = JsonSerializer.Serialize(identity, CloudWorkerJson.Options);
        var older = JsonSerializer.Serialize(
            new { identity.ServerId, identity.DeploymentId, identity.ProcessId, identity.ProcessStartedAt, identity.ExecutablePath },
            CloudWorkerJson.Options);

        Assert.Equal(27020, JsonSerializer.Deserialize<CounterStrike2ProcessIdentity>(json, CloudWorkerJson.Options)!.GamePort);
        Assert.DoesNotContain("gamePort", older, StringComparison.OrdinalIgnoreCase);
        Assert.Null(JsonSerializer.Deserialize<CounterStrike2ProcessIdentity>(older, CloudWorkerJson.Options)!.GamePort);
    }

    /// <summary>Verifies that the process controller records the port that follows the -port start argument.</summary>
    /// <param name="port">The argument that follows -port.</param>
    /// <param name="expected">The expected recorded game port.</param>
    [Theory]
    [InlineData("27020", 27020)]
    [InlineData("0", null)]
    [InlineData("70000", null)]
    [InlineData("-5", null)]
    [InlineData("abc", null)]
    public void ProcessController_ReadGamePort_ReadsTheStartPort(string port, int? expected)
        => Assert.Equal(expected, CounterStrike2ProcessController.ReadGamePort(["-dedicated", "-port", port, "+map", "x"]));

    /// <summary>Verifies that start arguments without a port record no game port.</summary>
    [Fact]
    public void ProcessController_ReadGamePort_WithoutPortArgument_ReturnsNull()
        => Assert.Null(CounterStrike2ProcessController.ReadGamePort(["-dedicated", "-port"]));

    private const string StartPassword = "rcon-secret-value-1f3a";

    private static async Task<CounterStrike2WorkerCommandExecutor> StartedExecutorAsync(
        TemporaryDirectory directory,
        ICounterStrike2RconClient rcon,
        int port,
        IA2sInfoClient? a2s = null)
    {
        var connection = Connection();
        var options = Options(directory.Path);
        var paths = new CounterStrike2RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.CounterStrike2.ExecutableRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ExecutablePath)!);
        await File.WriteAllTextAsync(paths.ExecutablePath, "fixture");
        Directory.CreateDirectory(paths.StateRoot);
        await File.WriteAllTextAsync(paths.PreparationMarkerPath, "fixture");
        var executor = new CounterStrike2WorkerCommandExecutor(
            connection,
            options,
            process: new FakeProcessController(connection, directory.Path),
            storage: new NoOpStorageTransfer(),
            rcon: rcon,
            a2s: a2s);
        var result = await executor.ExecuteAsync(StartCommand(connection, port), CancellationToken.None);
        Assert.True(result.Succeeded, result.Output);
        return executor;
    }

    private static CloudWorkerConnectionConfig Connection()
        => new()
        {
            ApiBaseUrl = "https://control.example/api/",
            WorkerId = Guid.NewGuid(),
            DeploymentId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            GameIdentifier = "cs2",
        };

    private static CloudWorkerOptions Options(string root)
        => new(
            Path.Combine(root, "bootstrap.json"),
            Path.Combine(root, "agent"),
            Path.Combine(root, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            true,
            false,
            new Arma3WorkerOptions(
                Path.Combine(root, "steamcmd.sh"),
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = Path.Combine(root, "tmp"),
            });

    private static WorkerCommandClaimResponse StartCommand(CloudWorkerConnectionConfig connection, int port)
    {
        var payload = new WorkerCommandPayload
        {
            ServerId = connection.ServerId,
            DeploymentId = connection.DeploymentId,
            GameIdentifier = "cs2",
            ActionIdentifier = "start",
            GracefulStopAttempts = 2,
            GracefulStopTimeoutSeconds = 30,
            Port = port,
            Terrain = new WorkerSelectedContent
            {
                Source = "BuiltIn", Identifier = "de_dust2", DisplayName = "de_dust2",
            },
            Scenario = new WorkerSelectedContent
            {
                Source = "BuiltIn", Identifier = "mode", DisplayName = "mode",
                Metadata = JsonSerializer.Serialize(new { gameType = 0, gameMode = 1 }),
            },
        };
        return new WorkerCommandClaimResponse
        {
            CommandId = Guid.NewGuid(),
            LifecycleJobId = Guid.NewGuid(),
            DeploymentId = connection.DeploymentId,
            WorkerId = connection.WorkerId,
            Sequence = 1,
            ActionIdentifier = "start",
            Payload = JsonSerializer.Serialize(payload, CloudWorkerJson.Options),
            Secrets = new Dictionary<string, string>
            {
                ["rcon-password"] = StartPassword,
                ["gslt"] = "gslt-token",
            },
            Attempt = 1,
        };
    }

    private sealed class FakeRconClient(string? reply) : ICounterStrike2RconClient
    {
        public int RequestCount { get; private set; }
        public int LastPort { get; private set; }
        public string? LastPassword { get; private set; }
        public TimeSpan LastTimeout { get; private set; }

        public Task<bool> RequestQuitAsync(
            int port, string password, TimeSpan timeout, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<string?> RequestStatusAsync(
            int port, string password, TimeSpan timeout, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastPort = port;
            LastPassword = password;
            LastTimeout = timeout;
            return Task.FromResult(reply);
        }
    }

    private sealed class FakeA2sInfoClient(A2sInfo? reply) : IA2sInfoClient
    {
        public int QueryCount { get; private set; }
        public int LastPort { get; private set; }
        public TimeSpan LastTimeout { get; private set; }

        public Task<A2sInfo?> QueryAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            QueryCount++;
            LastPort = port;
            LastTimeout = timeout;
            return Task.FromResult(reply);
        }
    }

    private sealed class FakeProcessController(
        CloudWorkerConnectionConfig connection,
        string root,
        bool running = false,
        int? gamePort = null) : ICounterStrike2ProcessController
    {
        private bool _running = running;
        private int? _gamePort = gamePort;

        public Task<CounterStrike2ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(_running
                ? new CounterStrike2ProcessObservation(
                    true,
                    new CounterStrike2ProcessIdentity(
                        connection.ServerId,
                        connection.DeploymentId,
                        4242,
                        DateTimeOffset.UtcNow,
                        Path.Combine(root, "cs2"),
                        _gamePort),
                    null,
                    null)
                : new CounterStrike2ProcessObservation(false, null, null, null));

        public Task<WorkerCommandExecutionResult> StartAsync(
            IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            _running = true;
            var portIndex = arguments.ToList().IndexOf("-port");
            _gamePort = portIndex >= 0
                ? int.Parse(arguments[portIndex + 1], System.Globalization.CultureInfo.InvariantCulture)
                : null;
            return Task.FromResult(WorkerCommandExecutionResult.Success(
                "started", processId: 4242, processStartedAt: DateTimeOffset.UtcNow));
        }

        public Task<WorkerCommandExecutionResult> StopAsync(
            bool force, TimeSpan timeout, int attempts, CancellationToken cancellationToken)
        {
            _running = false;
            return Task.FromResult(WorkerCommandExecutionResult.Success("stopped"));
        }

        public Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
            => StopAsync(true, TimeSpan.Zero, 1, cancellationToken);
    }

    private sealed class NoOpStorageTransfer : ICloudWorkerStorageTransfer
    {
        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command, string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishPreservationAsync(
            WorkerCommandClaimResponse command, string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>A loopback RCON server that authenticates, answers one command and sends the given chunks.</summary>
    private sealed class LoopbackRconServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public LoopbackRconServer(string password, string[] chunks)
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            CommandTask = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        using var client = await _listener.AcceptTcpClientAsync();
                        await using var stream = client.GetStream();
                        var auth = await ReadAsync(stream);
                        var accepted = auth.Body == password;
                        await WriteAsync(stream, accepted ? auth.RequestId : -1, 2, string.Empty);
                        if (!accepted) continue;
                        var request = await ReadAsync(stream);
                        foreach (var chunk in chunks)
                            await WriteAsync(stream, request.RequestId, 0, chunk);
                        await Task.Delay(500);
                        return (string?)request.Body;
                    }
                }
                catch (Exception exception) when (exception is ObjectDisposedException or SocketException
                                                      or IOException or InvalidOperationException)
                {
                    return null;
                }
            });
        }

        public int Port { get; }
        public Task<string?> CommandTask { get; }

        public void Dispose() => _listener.Stop();

        private static async Task<(int RequestId, string Body)> ReadAsync(Stream stream)
        {
            var length = new byte[4];
            await stream.ReadExactlyAsync(length);
            var payload = new byte[BinaryPrimitives.ReadInt32LittleEndian(length)];
            await stream.ReadExactlyAsync(payload);
            return (BinaryPrimitives.ReadInt32LittleEndian(payload), Encoding.UTF8.GetString(payload[8..^2]));
        }

        private static async Task WriteAsync(Stream stream, int requestId, int type, string body)
        {
            var text = Encoding.UTF8.GetBytes(body);
            var packet = new byte[14 + text.Length];
            BinaryPrimitives.WriteInt32LittleEndian(packet, packet.Length - 4);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), requestId);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), type);
            text.CopyTo(packet.AsSpan(12));
            await stream.WriteAsync(packet);
        }
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
