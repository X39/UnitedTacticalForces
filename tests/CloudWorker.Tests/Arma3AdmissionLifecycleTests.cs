using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Exercises final Arma admission and endpoint recovery without starting the game engine.</summary>
public sealed class Arma3AdmissionLifecycleTests
{
    /// <summary>Ensures restored configuration cannot retain obsolete client authorization at either launch path.</summary>
    /// <param name="action">The authorized operation that launches the process.</param>
    /// <returns>The asynchronous verification.</returns>
    [Theory]
    [InlineData("start")]
    [InlineData("restart")]
    public async Task Launch_ReconcilesRestoredAdmissionWithoutDiscardingUserSettings(string action)
    {
        using var fixture = new Fixture();
        var result = await fixture.CreateExecutor().ExecuteAsync(fixture.Command(action), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1, fixture.Process.StartCount);
        Assert.Contains("hostname = \"Preserved\";", fixture.Process.ConfigurationAtStart, StringComparison.Ordinal);
        Assert.Contains("headlessClients[] = {\"192.0.2.20\"};", fixture.Process.ConfigurationAtStart, StringComparison.Ordinal);
        Assert.Contains("localClient[] = {};", fixture.Process.ConfigurationAtStart, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.99", fixture.Process.ConfigurationAtStart, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Storage.RestoreCount);
        Assert.Contains("-serverMod=" + Arma3JoinAddon.GetModPath(fixture.Paths), fixture.Process.ArgumentsAtStart);
        Assert.True(File.Exists(Path.Combine(Arma3JoinAddon.GetModPath(fixture.Paths), "addons", "utf_hc_join.pbo")));
    }

    /// <summary>Refuses to launch when an overlay prevents reliable admission reconciliation.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task Launch_MalformedRestoredAdmissionDoesNotStartProcess()
    {
        using var fixture = new Fixture();
        fixture.Storage.RestoredConfiguration = "headlessClients[] = {\"192.0.2.99\";";

        var result = await fixture.CreateExecutor().ExecuteAsync(fixture.Command("start"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Process.StartCount);
    }

    /// <summary>Applies the new primary endpoint only through an explicit restart and leaves primary storage alone.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task HeadlessRestart_UsesNewEndpointWithoutRestoringPrimaryStorage()
    {
        using var fixture = new Fixture(headless: true);
        var executor = fixture.CreateExecutor();
        var started = await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorMessage);

        var restarted = await executor.ExecuteAsync(
            fixture.Command("restart", primaryAddress: "192.0.2.11"), CancellationToken.None);

        Assert.True(restarted.Succeeded, restarted.ErrorMessage);
        Assert.Contains("-connect=192.0.2.11", fixture.Process.ArgumentsAtStart);
        Assert.Equal(2, fixture.Process.StartCount);
        Assert.Equal(0, fixture.Storage.ResetCount);
        Assert.Equal(0, fixture.Storage.RestoreCount);
        Assert.DoesNotContain(fixture.Process.ArgumentsAtStart, argument => argument.StartsWith("-serverMod="));
        Assert.False(Directory.Exists(Arma3JoinAddon.GetModPath(fixture.Paths)));
    }

    /// <summary>Uses the same loopback address for colocated client connections as the managed allowlist.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task HeadlessStart_SharedHostConnectsThroughLoopback()
    {
        using var fixture = new Fixture(headless: true);
        var result = await fixture.CreateExecutor().ExecuteAsync(
            fixture.Command("start", clientAddress: "192.0.2.10"), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("-connect=127.0.0.1", fixture.Process.ArgumentsAtStart);
    }

    /// <summary>Adopts a running process after worker recovery only with the same saved endpoint context.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task Redelivery_UnchangedContextSurvivesExecutorRestartWithoutDuplicateLaunch()
    {
        using var fixture = new Fixture();
        var command = fixture.Command("start");
        var journal = new FlatFileWorkerCommandJournal(Path.Combine(fixture.Paths.AgentStatePath, "journal"));
        await journal.RecordReceivedAsync(command);
        await journal.RecordRunningAsync(command, "running");
        var entry = Assert.IsType<WorkerCommandJournalEntry>(await journal.ReadAsync(command.CommandId));
        var started = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorMessage);

        var result = await fixture.CreateExecutor().ReconcileRedeliveryAsync(command, entry, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("reconciled-running", result.Stage);
        Assert.Equal(1, fixture.Process.StartCount);
    }

    /// <summary>Does not silently reuse a live process after its required admission addresses change.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task Start_ChangedContextRequiresExplicitRestart()
    {
        using var fixture = new Fixture();
        var started = await fixture.CreateExecutor().ExecuteAsync(fixture.Command("start"), CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorMessage);
        var oldConfiguration = fixture.Process.ConfigurationAtStart;
        var executor = fixture.CreateExecutor();

        var rejected = await executor.ExecuteAsync(
            fixture.Command("start", clientAddress: "192.0.2.21"), CancellationToken.None);

        Assert.False(rejected.Succeeded);
        Assert.Equal("topology-restart-required", rejected.ErrorCode);
        Assert.Equal(1, fixture.Process.StartCount);
        Assert.Equal(oldConfiguration, await File.ReadAllTextAsync(fixture.Paths.ServerConfigurationPath));

        var restarted = await executor.ExecuteAsync(
            fixture.Command("restart", clientAddress: "192.0.2.21"), CancellationToken.None);

        Assert.True(restarted.Succeeded, restarted.ErrorMessage);
        Assert.Equal(2, fixture.Process.StartCount);
        Assert.Contains("headlessClients[] = {\"192.0.2.21\"};", fixture.Process.ConfigurationAtStart, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.20", fixture.Process.ConfigurationAtStart, StringComparison.Ordinal);
    }

    /// <summary>A process without recorded endpoint context requires an operator-authorized restart.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task Start_UnknownContextDoesNotAdoptExistingProcess()
    {
        using var fixture = new Fixture();
        fixture.Process.IsRunning = true;

        var result = await fixture.CreateExecutor().ExecuteAsync(fixture.Command("start"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("topology-restart-required", result.ErrorCode);
        Assert.Equal(0, fixture.Process.StartCount);
        Assert.Equal(0, fixture.Storage.RestoreCount);
    }

    /// <summary>Propagates actual process launch failure without reporting a client connection.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task HeadlessStart_ProcessFailureRemainsFailure()
    {
        using var fixture = new Fixture(headless: true);
        fixture.Process.FailStart = true;

        var result = await fixture.CreateExecutor().ExecuteAsync(fixture.Command("start"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("arma-exited", result.ErrorCode);
        Assert.False(fixture.Process.IsRunning);
    }

    /// <summary>A primary without an assigned HC still installs and loads the addon that reports players.</summary>
    /// <returns>The asynchronous always-on instrumentation verification.</returns>
    [Fact]
    public async Task Start_WithoutHeadlessAssignmentStillEnablesAddonForPlayers()
    {
        using var fixture = new Fixture();
        var command = WithoutHeadlessTopology(fixture.Command("start"));

        var result = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("-serverMod=" + Arma3JoinAddon.GetModPath(fixture.Paths), fixture.Process.ArgumentsAtStart);
        Assert.True(File.Exists(Path.Combine(Arma3JoinAddon.GetModPath(fixture.Paths), "addons", "utf_hc_join.pbo")));
        Assert.NotNull(await new Arma3JoinMonitor(fixture.Paths, TimeProvider.System)
            .ReadSessionAsync(CancellationToken.None));
    }

    /// <summary>Readiness verification still completes at once when no headless client is assigned.</summary>
    /// <returns>The asynchronous readiness verification.</returns>
    [Fact]
    public async Task VerifyReadiness_WithoutHeadlessAssignmentCompletesWithoutWaiting()
    {
        using var fixture = new Fixture(joinTimeout: TimeSpan.FromSeconds(30));
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(
            WithoutHeadlessTopology(fixture.Command("start")), CancellationToken.None)).Succeeded);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = await executor.ExecuteAsync(
            WithoutHeadlessTopology(fixture.Command("verify-readiness")), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("hc-not-required", result.Stage);
        Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(10));
    }

    /// <summary>Removes the headless-client endpoint so only the primary remains in the topology.</summary>
    /// <param name="command">A command created by the fixture.</param>
    /// <returns>The same command with a primary-only topology.</returns>
    private static WorkerCommandClaimResponse WithoutHeadlessTopology(WorkerCommandClaimResponse command)
    {
        var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(command.Payload, CloudWorkerJson.Options)!;
        return command with
        {
            Payload = JsonSerializer.Serialize(payload with
            {
                Topology = payload.Topology.Where(endpoint => endpoint.IsPrimary).ToArray(),
            }, CloudWorkerJson.Options),
        };
    }

    /// <summary>A live primary and a previously logged success cannot satisfy the new readiness command.</summary>
    /// <returns>The asynchronous readiness verification.</returns>
    [Fact]
    public async Task VerifyReadiness_RejectsReplayedPositiveEvidence()
    {
        using var fixture = new Fixture(joinTimeout: TimeSpan.FromMilliseconds(100));
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        var monitor = new Arma3JoinMonitor(fixture.Paths, TimeProvider.System);
        var session = Assert.IsType<Arma3JoinSession>(await monitor.ReadSessionAsync(CancellationToken.None));
        await File.WriteAllTextAsync(fixture.Paths.RuntimeLogPath, $"UTFHC|1|{session.Nonce}|1|3|1\n");

        var result = await executor.ExecuteAsync(fixture.Command("verify-readiness"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("hc-join-timeout", result.ErrorCode);
        Assert.True(fixture.Process.IsRunning);
        Assert.Null((await monitor.ReadSessionAsync(CancellationToken.None))!.VerifiedProcessId);
    }

    /// <summary>Fresh server evidence arms health monitoring and survives executor reconstruction without relaunch.</summary>
    /// <returns>The asynchronous engine-evidence simulation.</returns>
    [Fact]
    public async Task VerifyReadiness_FreshEvidenceSucceedsAndExpiryReportsFailedWithRealPid()
    {
        using var fixture = new Fixture(joinTimeout: TimeSpan.FromSeconds(3));
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        Assert.Contains("-serverMod=" + Arma3JoinAddon.GetModPath(fixture.Paths), fixture.Process.ArgumentsAtStart);
        var monitor = new Arma3JoinMonitor(fixture.Paths, TimeProvider.System);
        var session = Assert.IsType<Arma3JoinSession>(await monitor.ReadSessionAsync(CancellationToken.None));
        var verification = executor.ExecuteAsync(fixture.Command("verify-readiness"), CancellationToken.None);
        for (var sequence = 1; sequence <= 20 && !verification.IsCompleted; sequence++)
        {
            await Task.Delay(30);
            await File.AppendAllTextAsync(fixture.Paths.RuntimeLogPath, $"UTFHC|1|{session.Nonce}|{sequence}|3|1\n");
        }
        var result = await verification;
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("hc-joined", result.Stage);

        executor = fixture.CreateExecutor();
        await executor.ObserveRuntimeAsync(CancellationToken.None);
        await Task.Delay(80);
        var expired = Assert.IsType<WorkerRuntimeStatus>(await executor.ObserveRuntimeAsync(CancellationToken.None));
        Assert.Equal(123, expired.ProcessId);
        Assert.Equal(EWorkerWorkloadState.Failed, Assert.Single(expired.Workloads).State);
        Assert.Equal(123, Assert.Single(expired.Workloads).ProcessId);
        Assert.Equal(1, fixture.Process.StartCount);
    }

    /// <summary>A new primary incarnation cannot consume the preceding launch's addon output.</summary>
    /// <returns>The asynchronous restart verification.</returns>
    [Fact]
    public async Task VerifyReadiness_RestartRejectsPreviousLaunchNonce()
    {
        using var fixture = new Fixture(joinTimeout: TimeSpan.FromMilliseconds(100));
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        var monitor = new Arma3JoinMonitor(fixture.Paths, TimeProvider.System);
        var previous = Assert.IsType<Arma3JoinSession>(await monitor.ReadSessionAsync(CancellationToken.None));
        Assert.True((await executor.ExecuteAsync(fixture.Command("restart"), CancellationToken.None)).Succeeded);
        var current = Assert.IsType<Arma3JoinSession>(await monitor.ReadSessionAsync(CancellationToken.None));
        Assert.NotEqual(previous.Nonce, current.Nonce);
        await File.WriteAllTextAsync(fixture.Paths.RuntimeLogPath, $"UTFHC|1|{previous.Nonce}|99|3|1\n");

        var result = await executor.ExecuteAsync(fixture.Command("verify-readiness"), CancellationToken.None);

        Assert.Equal("hc-join-timeout", result.ErrorCode);
        Assert.Equal(2, fixture.Process.StartCount);
    }

    /// <summary>A stopped process fails immediately, and command cancellation never arms a readiness claim.</summary>
    /// <returns>The asynchronous failure and cancellation checks.</returns>
    [Fact]
    public async Task VerifyReadiness_ProcessExitAndCancellationDoNotPass()
    {
        using var fixture = new Fixture();
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        using var cancellation = new CancellationTokenSource();
        var waiting = executor.ExecuteAsync(fixture.Command("verify-readiness"), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        fixture.Process.IsRunning = false;
        var result = await executor.ExecuteAsync(fixture.Command("verify-readiness"), CancellationToken.None);
        Assert.Equal("hc-primary-not-running", result.ErrorCode);
    }

    /// <summary>A running primary with a fresh addon record reports the engine's human count without capacity.</summary>
    /// <returns>The asynchronous player observation.</returns>
    [Fact]
    public async Task ObservePlayers_PrimaryWithFreshRecordReportsCountWithoutCapacity()
    {
        using var fixture = new Fixture();
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        var monitor = new Arma3JoinMonitor(fixture.Paths, TimeProvider.System);
        var session = Assert.IsType<Arma3JoinSession>(await monitor.ReadSessionAsync(CancellationToken.None));
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring,
            (await executor.ObservePlayersAsync(CancellationToken.None)).UnavailableReason);
        await File.WriteAllTextAsync(fixture.Paths.RuntimeLogPath,
            $"UTFHC|1|{session.Nonce}|9|3|1\nUTFPL|1|{session.Nonce}|4|5\n");

        var reading = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Equal(5, reading.Count);
        Assert.Null(reading.Capacity);
        Assert.Null(reading.UnavailableReason);
    }

    /// <summary>Without a launch session or a running process the executor never invents a player count.</summary>
    /// <returns>The asynchronous player observation.</returns>
    [Fact]
    public async Task ObservePlayers_WithoutSessionOrProcessReportsMeasuring()
    {
        using var fixture = new Fixture();
        var executor = fixture.CreateExecutor();
        await File.WriteAllTextAsync(fixture.Paths.RuntimeLogPath, $"UTFPL|1|{new string('a', 32)}|1|5\n");
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring,
            (await executor.ObservePlayersAsync(CancellationToken.None)).UnavailableReason);

        fixture.Process.IsRunning = true;
        var running = await executor.ObservePlayersAsync(CancellationToken.None);
        Assert.Null(running.Count);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, running.UnavailableReason);

        fixture.Process.IsRunning = false;
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        await File.WriteAllTextAsync(fixture.Paths.RuntimeLogPath,
            $"UTFPL|1|{(await new Arma3JoinMonitor(fixture.Paths, TimeProvider.System).ReadSessionAsync(CancellationToken.None))!.Nonce}|1|5\n");
        Assert.Equal(5, (await executor.ObservePlayersAsync(CancellationToken.None)).Count);
        fixture.Process.IsRunning = false;
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring,
            (await executor.ObservePlayersAsync(CancellationToken.None)).UnavailableReason);
    }

    /// <summary>A headless-client host has no player source of its own.</summary>
    /// <returns>The asynchronous player observation.</returns>
    [Fact]
    public async Task ObservePlayers_HeadlessClientHostReportsGameDoesNotReport()
    {
        using var fixture = new Fixture(headless: true);
        var executor = fixture.CreateExecutor();
        Assert.True((await executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);

        var reading = await executor.ObservePlayersAsync(CancellationToken.None);

        Assert.Null(reading.Count);
        Assert.Equal(WorkerMetricUnavailableReasons.GameDoesNotReport, reading.UnavailableReason);
    }

    // These private test doubles are nested because their lifetimes and assertions belong only to this fixture.
    /// <summary>Creates a prepared deployment and injectable process/storage doubles without running native tools.</summary>
    private sealed class Fixture : IDisposable
    {
        /// <summary>Owns the temporary deployment and state directories.</summary>
        private readonly TemporaryDirectory _directory = new();
        /// <summary>Supplies deployment attribution to commands and the fake process.</summary>
        private readonly CloudWorkerBootstrapConfig _bootstrap;
        /// <summary>Provides local runtime paths without Steam credentials or native execution.</summary>
        private readonly CloudWorkerOptions _options;

        /// <summary>Creates a prepared primary or auxiliary worker fixture.</summary>
        /// <param name="headless">Whether this worker is assigned the client role.</param>
        /// <param name="joinTimeout">Optional short deadline for readiness failure tests.</param>
        public Fixture(bool headless = false, TimeSpan? joinTimeout = null)
        {
            _bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap() with
            {
                ServiceIdentifier = headless ? "headless-client" : "game-server",
            };
            _options = new CloudWorkerOptions(
                Path.Combine(_directory.Path, "bootstrap.json"), Path.Combine(_directory.Path, "state"),
                Path.Combine(_directory.Path, "runtime"), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15),
                true, false,
                new Arma3WorkerOptions(Path.Combine(_directory.Path, "steamcmd"), "anonymous", null, null,
                    "arma3server_x64", TimeSpan.FromSeconds(30))
                {
                    TemporaryDirectory = Path.Combine(_directory.Path, "tmp"),
                    HeadlessClientJoinTimeout = joinTimeout ?? TimeSpan.FromSeconds(2),
                    HeadlessClientEvidenceGrace = TimeSpan.FromMilliseconds(50),
                });
            Paths = new Arma3RuntimePaths(_options.RuntimeRoot, _options.StateDirectory,
                _bootstrap.DeploymentId, _options.Arma3.ExecutableName);
            Paths.EnsureDirectories();
            File.WriteAllText(Paths.ExecutablePath, "prepared");
            File.WriteAllText(Paths.PreparationMarkerPath, "prepared");
            Process = new ProcessController(_bootstrap, Paths);
            Storage = new StorageTransfer(Paths);
        }

        /// <summary>Gets the prepared runtime paths.</summary>
        /// <value>Paths shared by successive executor instances.</value>
        public Arma3RuntimePaths Paths { get; }
        /// <summary>Gets the process double retained across worker recovery.</summary>
        /// <value>The observable fake process controller.</value>
        public ProcessController Process { get; }
        /// <summary>Gets the persistent overlay double.</summary>
        /// <value>The storage operations and restored content.</value>
        public StorageTransfer Storage { get; }

        /// <summary>Constructs another worker instance over the same deployment state.</summary>
        /// <returns>The executor with isolated fake process and storage dependencies.</returns>
        public Arma3WorkerCommandExecutor CreateExecutor()
            => new(_bootstrap.ToConnectionConfig(), _options, processController: Process, storageTransfer: Storage);

        /// <summary>Creates an immutable command with independently selectable current endpoint addresses.</summary>
        /// <param name="action">The requested lifecycle operation.</param>
        /// <param name="primaryAddress">The primary endpoint for the operation.</param>
        /// <param name="clientAddress">The authorized client source address.</param>
        /// <returns>A deployment-attributed worker command.</returns>
        public WorkerCommandClaimResponse Command(string action, string primaryAddress = "192.0.2.10",
            string clientAddress = "192.0.2.20")
        {
            var primary = _bootstrap.ServiceIdentifier == "game-server";
            var payload = new WorkerCommandPayload
            {
                ServerId = _bootstrap.ServerId, DeploymentId = _bootstrap.DeploymentId,
                GameIdentifier = "arma3", ActionIdentifier = action,
                ServiceIdentifier = _bootstrap.ServiceIdentifier, IsPrimaryService = primary,
                PreserveOnStop = primary, GracefulStopAttempts = 2, GracefulStopTimeoutSeconds = 30,
                Topology =
                [
                    new WorkerNodeEndpoint
                    {
                        ServiceIdentifier = "game-server", Address = primaryAddress, Port = 2302, IsPrimary = true,
                    },
                    new WorkerNodeEndpoint { ServiceIdentifier = "headless-client", Address = clientAddress },
                ],
            };
            return FlatFileWorkerCommandJournalTests.CreateCommand(
                _bootstrap.WorkerId, _bootstrap.DeploymentId, _bootstrap.ServerId, action) with
            {
                Payload = JsonSerializer.Serialize(payload, CloudWorkerJson.Options),
            };
        }

        /// <inheritdoc />
        public void Dispose() => _directory.Dispose();
    }

    /// <summary>Models attributed process survival while capturing the configuration loaded at launch.</summary>
    /// <param name="bootstrap">The deployment identity assigned to the process.</param>
    /// <param name="paths">The configuration and executable paths used by the worker.</param>
    private sealed class ProcessController(CloudWorkerBootstrapConfig bootstrap, Arma3RuntimePaths paths)
        : IArma3ProcessController
    {
        /// <summary>Tracks the current process incarnation across observations.</summary>
        private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
        /// <summary>Gets or sets whether the attributed process is alive.</summary>
        /// <value>The fake runtime liveness state.</value>
        public bool IsRunning { get; set; }
        /// <summary>Gets or sets whether the next launch exits immediately.</summary>
        /// <value>True to return a process failure.</value>
        public bool FailStart { get; set; }
        /// <summary>Gets the number of actual process launch calls.</summary>
        /// <value>The counter used to reject duplicate spawning.</value>
        public int StartCount { get; private set; }
        /// <summary>Gets the primary configuration seen at the last launch.</summary>
        /// <value>The captured configuration, or an empty string when absent.</value>
        public string ConfigurationAtStart { get; private set; } = string.Empty;
        /// <summary>Gets the arguments used by the last process launch.</summary>
        /// <value>The immutable argument snapshot.</value>
        public IReadOnlyList<string> ArgumentsAtStart { get; private set; } = [];
        /// <inheritdoc />
        public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Arma3ProcessObservation(IsRunning,
                IsRunning ? new Arma3ProcessIdentity(bootstrap.ServerId, bootstrap.DeploymentId, 123,
                    _startedAt, paths.ExecutablePath) : null, null, null));
        /// <inheritdoc />
        public Task<WorkerCommandExecutionResult> StartAsync(IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            StartCount++;
            ArgumentsAtStart = arguments.ToArray();
            ConfigurationAtStart = File.Exists(paths.ServerConfigurationPath)
                ? File.ReadAllText(paths.ServerConfigurationPath) : string.Empty;
            IsRunning = !FailStart;
            _startedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(FailStart
                ? WorkerCommandExecutionResult.Failure("start-exited", "arma-exited", "The process exited.")
                : WorkerCommandExecutionResult.Success("started", processId: 123, processStartedAt: _startedAt));
        }
        /// <inheritdoc />
        public Task<WorkerCommandExecutionResult> StopAsync(bool force, TimeSpan timeout, int gracefulAttempts,
            CancellationToken cancellationToken)
        {
            IsRunning = false;
            return Task.FromResult(WorkerCommandExecutionResult.Success("stopped"));
        }
    }

    /// <summary>Restores stale configuration so final reconciliation is exercised through the lifecycle path.</summary>
    /// <param name="paths">The deployment configuration destination.</param>
    private sealed class StorageTransfer(Arma3RuntimePaths paths) : ICloudWorkerStorageTransfer
    {
        /// <summary>Gets or sets the persistent configuration supplied by restore.</summary>
        /// <value>A stale allowlist with unrelated user configuration.</value>
        public string RestoredConfiguration { get; set; }
            = "hostname = \"Preserved\";\nheadlessClients[] = {\"192.0.2.99\"};\nlocalClient[] = {\"192.0.2.99\"};\n";
        /// <summary>Gets the number of persistent overlay resets.</summary>
        /// <value>The reset count.</value>
        public int ResetCount { get; private set; }
        /// <summary>Gets the number of persistent snapshot restores.</summary>
        /// <value>The restore count.</value>
        public int RestoreCount { get; private set; }
        /// <inheritdoc />
        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
        {
            ResetCount++;
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public async Task ApplyPersistentSnapshotAsync(WorkerCommandClaimResponse command, string deploymentRoot,
            CancellationToken cancellationToken)
        {
            RestoreCount++;
            await File.WriteAllTextAsync(paths.ServerConfigurationPath, RestoredConfiguration, cancellationToken);
        }
        /// <inheritdoc />
        public Task PublishPreservationAsync(WorkerCommandClaimResponse command, string deploymentRoot,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
