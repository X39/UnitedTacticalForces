using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Exercises the two-process lifecycle through the production Arma command executor.</summary>
public sealed class Arma3ColocatedSupervisionTests
{
    /// <summary>Prepares each mod once while retaining separate role arguments, profiles, and local admission.</summary>
    /// <returns>The asynchronous lifecycle verification.</returns>
    [Fact]
    public async Task Start_SharesPreparationAndSeparatesLocalWorkloadIdentity()
    {
        using var fixture = new Fixture();
        var result = await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1, fixture.Runner.RunCount);
        foreach (var id in new[] { "101", "102", "103" })
            Assert.Single(Regex.Matches(fixture.Runner.Script, $"workshop_download_item 107410 {id}\\b"));
        var primaryMods = Assert.Single(fixture.Primary.Arguments, q => q.StartsWith("-mod=", StringComparison.Ordinal));
        var auxiliaryMods = Assert.Single(fixture.Auxiliary.Arguments, q => q.StartsWith("-mod=", StringComparison.Ordinal));
        Assert.Contains(fixture.Paths.GetSteamWorkshopItemPath(101), primaryMods);
        Assert.Contains(fixture.Paths.GetSteamWorkshopItemPath(101), auxiliaryMods);
        Assert.Contains(fixture.Paths.GetSteamWorkshopItemPath(102), primaryMods);
        Assert.DoesNotContain(fixture.Paths.GetSteamWorkshopItemPath(103), primaryMods);
        Assert.Contains(fixture.Paths.GetSteamWorkshopItemPath(103), auxiliaryMods);
        Assert.DoesNotContain(fixture.Paths.GetSteamWorkshopItemPath(102), auxiliaryMods);
        Assert.Contains("-connect=127.0.0.1", fixture.Auxiliary.Arguments);
        Assert.Contains("-profiles=" + fixture.AuxiliaryPaths.ProfilePath, fixture.Auxiliary.Arguments);
        Assert.Contains("-profiles=" + fixture.Paths.ProfilePath, fixture.Primary.Arguments);
        Assert.NotEqual(fixture.Primary.Identity!.ProcessId, fixture.Auxiliary.Identity!.ProcessId);
        Assert.Equal(["start-game-server", "start-headless-client"], fixture.Events.Where(q => q.StartsWith("start-")));
        Assert.Contains("hostname = \"restored\";", fixture.Primary.ConfigurationAtStart);
        Assert.Contains("headlessClients[] = {\"127.0.0.1\"};", fixture.Primary.ConfigurationAtStart);
        Assert.Contains("localClient[] = {\"127.0.0.1\"};", fixture.Primary.ConfigurationAtStart);
        Assert.DoesNotContain("192.0.2.99", fixture.Primary.ConfigurationAtStart);
        Assert.Equal(2, result.Workloads.Count);
        Assert.All(result.Workloads, q => Assert.Equal(EWorkerWorkloadState.Running, q.State));
        Assert.DoesNotContain("joined", result.Stage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies colocated admission separately from launch and preserves both processes across worker recovery.</summary>
    /// <returns>A task completing after fresh primary evidence verifies both original and reconstructed executors.</returns>
    [Fact]
    public async Task VerifyReadiness_UsesPrimaryEvidenceWithoutRelaunchingColocatedProcesses()
    {
        using var fixture = new Fixture(TimeSpan.FromSeconds(2));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = await fixture.Executor.ExecuteAsync(fixture.Command("start"), timeout.Token);
        Assert.True(started.Succeeded, started.ErrorMessage);
        Assert.DoesNotContain("joined", started.Stage, StringComparison.OrdinalIgnoreCase);
        var sessionPath = Path.Combine(fixture.Paths.AgentStatePath, "arma3-join-session.json");
        var session = JsonSerializer.Deserialize<Arma3JoinSession>(await File.ReadAllTextAsync(sessionPath),
            CloudWorkerJson.Options)!;
        var command = fixture.Command("verify-readiness");
        var verification = fixture.Executor.ExecuteAsync(command, timeout.Token);
        await PublishFreshJoinEvidenceAsync(fixture.Paths, session.Nonce, verification, 1, timeout.Token);
        var verified = await verification;
        Assert.True(verified.Succeeded, verified.ErrorMessage);
        Assert.Equal("hc-joined", verified.Stage);
        Assert.Equal(fixture.Primary.Identity!.ProcessId, verified.ProcessId);

        var recovered = fixture.CreateExecutor();
        var repeated = recovered.ExecuteAsync(command, timeout.Token);
        await PublishFreshJoinEvidenceAsync(fixture.Paths, session.Nonce, repeated, 100, timeout.Token);
        Assert.True((await repeated).Succeeded);
        Assert.Equal(1, fixture.Primary.LaunchCount);
        Assert.Equal(1, fixture.Auxiliary.LaunchCount);
        Assert.Equal(1, fixture.Runner.RunCount);
        Assert.True(fixture.Primary.Running);
        Assert.True(fixture.Auxiliary.Running);
        Assert.Equal(session.Nonce, JsonSerializer.Deserialize<Arma3JoinSession>(await File.ReadAllTextAsync(sessionPath),
            CloudWorkerJson.Options)!.Nonce);
    }

    /// <summary>Living colocated processes and a stale successful record cannot satisfy the readiness command.</summary>
    /// <returns>A task completing after a bounded explicit readiness failure without duplicate process launch.</returns>
    [Fact]
    public async Task VerifyReadiness_RejectsStaleColocatedEvidenceDespiteRunningProcesses()
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(100));
        var started = await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorMessage);
        var session = JsonSerializer.Deserialize<Arma3JoinSession>(await File.ReadAllTextAsync(
            Path.Combine(fixture.Paths.AgentStatePath, "arma3-join-session.json")), CloudWorkerJson.Options)!;
        await File.WriteAllTextAsync(fixture.Paths.RuntimeLogPath, $"UTFHC|1|{session.Nonce}|1|17|1\n");

        var result = await fixture.CreateExecutor().ExecuteAsync(fixture.Command("verify-readiness"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("hc-join-timeout", result.ErrorCode);
        Assert.Equal(1, fixture.Primary.LaunchCount);
        Assert.Equal(1, fixture.Auxiliary.LaunchCount);
        // The API owns provider cleanup after this failed follow-up command.
        Assert.True(fixture.Primary.Running);
        Assert.True(fixture.Auxiliary.Running);
    }

    /// <summary>Emulates advancing engine output while the real readiness command is observing its log.</summary>
    /// <param name="paths">The primary process output location.</param>
    /// <param name="nonce">The current launch correlation marker.</param>
    /// <param name="verification">The running readiness command.</param>
    /// <param name="sequence">The first sequence greater than all prior fixture output.</param>
    /// <param name="cancellationToken">Cancels a stuck test rather than leaving an output loop running.</param>
    /// <returns>A task completing when verification finishes.</returns>
    private static async Task PublishFreshJoinEvidenceAsync(
        Arma3RuntimePaths paths, string nonce, Task<WorkerCommandExecutionResult> verification, long sequence,
        CancellationToken cancellationToken)
    {
        while (!verification.IsCompleted)
        {
            await File.AppendAllTextAsync(paths.RuntimeLogPath, $"UTFHC|1|{nonce}|{sequence++}|17|1\n", cancellationToken);
            await Task.Delay(20, cancellationToken);
        }
    }

    /// <summary>A failed dependent launch cannot leave a successful primary-only deployment.</summary>
    /// <returns>The asynchronous failure verification.</returns>
    [Fact]
    public async Task AuxiliaryLaunchFailure_CompensatesPrimary()
    {
        using var fixture = new Fixture();
        fixture.Auxiliary.FailStart = true;

        var result = await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(1, fixture.Primary.LaunchCount);
        Assert.False(fixture.Primary.Running);
        Assert.False(fixture.Auxiliary.Running);
        Assert.Contains("terminate-game-server", fixture.Events);
        Assert.Equal(0, fixture.Storage.PublicationCount);
    }

    /// <summary>Attempts the primary shutdown even when stopping the dependent fails or throws.</summary>
    /// <param name="throwOnStop">Whether the dependent throws instead of returning failure.</param>
    /// <returns>The asynchronous shutdown verification.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_AuxiliaryFailureStillAttemptsPrimaryWithoutPublishing(bool throwOnStop)
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();
        fixture.Auxiliary.FailStop = !throwOnStop;
        fixture.Auxiliary.ThrowOnStop = throwOnStop;

        var result = await fixture.Executor.ExecuteAsync(fixture.Command("stop"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(["stop-headless-client", "stop-game-server"], fixture.Events);
        Assert.False(fixture.Primary.Running);
        Assert.True(fixture.Auxiliary.Running);
        Assert.Equal(0, fixture.Storage.PublicationCount);
    }

    /// <summary>Publishes only after both writers exit and does not repeat a completed stop publication.</summary>
    /// <returns>The asynchronous preservation verification.</returns>
    [Fact]
    public async Task Stop_VerifiesBothWritersBeforeSinglePublication()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();
        var stop = fixture.Command("stop");

        var first = await fixture.Executor.ExecuteAsync(stop, CancellationToken.None);
        var redelivered = await fixture.CreateExecutor().ExecuteAsync(stop, CancellationToken.None);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.True(redelivered.Succeeded, redelivered.ErrorMessage);
        Assert.Equal(1, fixture.Storage.PublicationCount);
        Assert.Equal(["stop-headless-client", "stop-game-server", "publish"], fixture.Events);
        Assert.All(first.Workloads, q => Assert.Equal(EWorkerWorkloadState.Stopped, q.State));
    }

    /// <summary>Replaces both processes in dependency order and resumes without repeating either launch.</summary>
    /// <returns>The asynchronous restart and redelivery verification.</returns>
    [Fact]
    public async Task Restart_OrdersBothRolesAndSurvivesWorkerReconstruction()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        var oldPrimary = fixture.Primary.Identity;
        var oldAuxiliary = fixture.Auxiliary.Identity;
        fixture.Events.Clear();
        var restart = fixture.Command("restart");

        var first = await fixture.Executor.ExecuteAsync(restart, CancellationToken.None);
        var redelivered = await fixture.CreateExecutor().ExecuteAsync(restart, CancellationToken.None);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.True(redelivered.Succeeded, redelivered.ErrorMessage);
        Assert.Equal(["stop-headless-client", "stop-game-server", "prepare", "restore", "start-game-server",
            "start-headless-client"], fixture.Events);
        Assert.Equal(2, fixture.Primary.LaunchCount);
        Assert.Equal(2, fixture.Auxiliary.LaunchCount);
        Assert.NotEqual(oldPrimary!.ProcessId, fixture.Primary.Identity!.ProcessId);
        Assert.NotEqual(oldAuxiliary!.ProcessId, fixture.Auxiliary.Identity!.ProcessId);
        Assert.Equal(0, fixture.Storage.PublicationCount);
    }

    /// <summary>Adopts both existing processes after the worker restarts without repeating preparation or launch.</summary>
    /// <returns>The asynchronous replay verification.</returns>
    [Fact]
    public async Task StartRedelivery_AfterWorkerRestartDoesNotDuplicateProcesses()
    {
        using var fixture = new Fixture();
        var command = fixture.Command("start");
        Assert.True((await fixture.Executor.ExecuteAsync(command, CancellationToken.None)).Succeeded);
        fixture.Events.Clear();

        var result = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Empty(fixture.Events);
        Assert.Equal(1, fixture.Primary.LaunchCount);
        Assert.Equal(1, fixture.Auxiliary.LaunchCount);
        Assert.Equal(1, fixture.Runner.RunCount);
    }

    /// <summary>Completes compensation when the worker crashed after durably recording failure.</summary>
    /// <returns>The asynchronous crash recovery verification.</returns>
    [Fact]
    public async Task FailedPhaseRedelivery_StopsSurvivingProcessesWithoutRelaunching()
    {
        using var fixture = new Fixture();
        var command = fixture.Command("start");
        Assert.True((await fixture.Executor.ExecuteAsync(command, CancellationToken.None)).Succeeded);
        await fixture.SetPhaseAsync("Failed");
        fixture.Events.Clear();

        var result = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(fixture.Primary.Running);
        Assert.False(fixture.Auxiliary.Running);
        Assert.Equal(["terminate-headless-client", "terminate-game-server"], fixture.Events);
        Assert.Equal(1, fixture.Primary.LaunchCount);
        Assert.Equal(1, fixture.Auxiliary.LaunchCount);
    }

    /// <summary>Resumes preparation or an uncertain primary launch without duplicating a surviving primary.</summary>
    /// <param name="phase">The durable phase left before the simulated worker crash.</param>
    /// <param name="keepPrimary">Whether the primary process survived the interrupted launch.</param>
    /// <returns>The asynchronous phase recovery verification.</returns>
    [Theory]
    [InlineData("Preparing", false)]
    [InlineData("PrimaryStarting", true)]
    public async Task PartialStartRecovery_ResumesOnlyUnfinishedPhases(string phase, bool keepPrimary)
    {
        using var fixture = new Fixture();
        var command = fixture.Command("start");
        Assert.True((await fixture.Executor.ExecuteAsync(command, CancellationToken.None)).Succeeded);
        await fixture.Auxiliary.TerminateAsync(CancellationToken.None);
        if (!keepPrimary)
            await fixture.Primary.TerminateAsync(CancellationToken.None);
        await fixture.SetPhaseAsync(phase);
        fixture.Events.Clear();

        var result = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(fixture.Primary.Running);
        Assert.True(fixture.Auxiliary.Running);
        Assert.Equal(keepPrimary ? 1 : 2, fixture.Primary.LaunchCount);
        Assert.Equal(2, fixture.Auxiliary.LaunchCount);
        Assert.Equal(keepPrimary ? 1 : 2, fixture.Runner.RunCount);
        if (keepPrimary)
            Assert.Equal(["start-headless-client"], fixture.Events);
    }

    /// <summary>Withdraws group liveness and stops the dependent when the primary exits after startup.</summary>
    /// <returns>The asynchronous runtime observation verification.</returns>
    [Fact]
    public async Task RuntimePrimaryLoss_StopsDependentAndReportsBothMissing()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        await fixture.Primary.TerminateAsync(CancellationToken.None);
        fixture.Events.Clear();

        var observation = await fixture.CreateExecutor().ObserveRuntimeAsync(CancellationToken.None);

        Assert.NotNull(observation);
        Assert.Null(observation.ProcessId);
        Assert.Null(observation.ProcessStartedAt);
        Assert.All(observation.Workloads, q => Assert.Equal(EWorkerWorkloadState.Stopped, q.State));
        Assert.False(fixture.Auxiliary.Running);
        Assert.Equal(["terminate-headless-client"], fixture.Events);
    }

    /// <summary>Uses persisted supervision when cleanup can no longer supply placement or provider endpoints.</summary>
    /// <param name="action">The authorized cleanup operation.</param>
    /// <returns>The asynchronous fallback verification.</returns>
    [Theory]
    [InlineData("stop")]
    [InlineData("fault-stop")]
    [InlineData("terminate")]
    public async Task CleanupWithoutTopology_StopsBothPersistedWorkloads(string action)
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();
        var command = fixture.Command(action);
        var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(command.Payload, CloudWorkerJson.Options)!;
        command = command with
        {
            Payload = JsonSerializer.Serialize(payload with { Topology = [], Workloads = [] }, CloudWorkerJson.Options),
        };

        var result = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(fixture.Primary.Running);
        Assert.False(fixture.Auxiliary.Running);
        Assert.Equal(action == "terminate" ? 0 : 1, fixture.Storage.PublicationCount);
        var verb = action == "terminate" ? "terminate-" : "stop-";
        Assert.Equal(verb + "headless-client", fixture.Events[0]);
        Assert.Equal(verb + "game-server", fixture.Events[1]);
        Assert.All(result.Workloads, q => Assert.Equal(EWorkerWorkloadState.Stopped, q.State));
    }

    /// <summary>Reports an expired fault deadline without claiming shutdown or publishing unverified data.</summary>
    /// <returns>The asynchronous deadline verification.</returns>
    [Fact]
    public async Task FaultStop_ExpiredDeadlineDoesNotPublishOrReportSuccessfulCleanup()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();
        var command = fixture.Command("fault-stop");
        var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(command.Payload, CloudWorkerJson.Options)!;
        command = command with
        {
            Payload = JsonSerializer.Serialize(payload with
            {
                Topology = [], Workloads = [], FaultEvidenceDeadline = DateTimeOffset.UtcNow.AddSeconds(-1),
            }, CloudWorkerJson.Options),
        };

        var result = await fixture.CreateExecutor().ExecuteAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("fault-evidence-deadline-expired", result.ErrorCode);
        Assert.Empty(fixture.Events);
        Assert.Equal(0, fixture.Storage.PublicationCount);
        Assert.True(fixture.Primary.Running);
        Assert.True(fixture.Auxiliary.Running);
    }

    /// <summary>Prepares shared content once while both consumers are stopped, then restores the running group.</summary>
    /// <returns>The asynchronous update verification.</returns>
    [Fact]
    public async Task Update_StopsBothConsumersBeforeOneSharedPreparation()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();

        var result = await fixture.Executor.ExecuteAsync(fixture.Command("update"), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(["stop-headless-client", "stop-game-server", "prepare", "start-game-server",
            "start-headless-client"], fixture.Events);
        Assert.Equal(2, fixture.Runner.RunCount);
        Assert.Equal(2, fixture.Primary.LaunchCount);
        Assert.Equal(2, fixture.Auxiliary.LaunchCount);
        Assert.Equal(0, fixture.Storage.PublicationCount);
        Assert.All(result.Workloads, q => Assert.Equal(EWorkerWorkloadState.Running, q.State));
    }

    /// <summary>Reports temporary process absence during maintenance without competing with the active mutation owner.</summary>
    /// <returns>The asynchronous command and heartbeat concurrency verification.</returns>
    [Fact]
    public async Task Heartbeat_DuringPausedUpdateDoesNotTerminateDependentOrReplacement()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var preparationReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuePreparation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Runner.BeforePreparation = async cancellationToken =>
        {
            preparationReached.TrySetResult();
            await continuePreparation.Task.WaitAsync(cancellationToken);
        };
        var update = fixture.Executor.ExecuteAsync(fixture.Command("update"), timeout.Token);
        try
        {
            await preparationReached.Task.WaitAsync(timeout.Token);

            var heartbeat = await fixture.Executor.ObserveRuntimeAsync(timeout.Token).WaitAsync(timeout.Token);

            Assert.NotNull(heartbeat);
            Assert.All(heartbeat.Workloads, q => Assert.Equal(EWorkerWorkloadState.Stopped, q.State));
            Assert.DoesNotContain(fixture.Events, q => q.StartsWith("terminate-", StringComparison.Ordinal));
        }
        finally
        {
            continuePreparation.TrySetResult();
        }
        var result = await update.WaitAsync(timeout.Token);
        var after = await fixture.Executor.ObserveRuntimeAsync(timeout.Token);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(after);
        Assert.All(after.Workloads, q => Assert.Equal(EWorkerWorkloadState.Running, q.State));
        Assert.DoesNotContain(fixture.Events, q => q.StartsWith("terminate-", StringComparison.Ordinal));
        Assert.Equal(2, fixture.Primary.LaunchCount);
        Assert.Equal(2, fixture.Auxiliary.LaunchCount);
    }

    /// <summary>Does not restart previously stopped workloads merely because installation completed.</summary>
    /// <returns>The asynchronous stopped-maintenance verification.</returns>
    [Fact]
    public async Task Install_AfterStopLeavesBothWorkloadsStopped()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("start"), CancellationToken.None)).Succeeded);
        Assert.True((await fixture.Executor.ExecuteAsync(fixture.Command("stop"), CancellationToken.None)).Succeeded);
        fixture.Events.Clear();

        var result = await fixture.Executor.ExecuteAsync(fixture.Command("install"), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(["stop-headless-client", "stop-game-server", "prepare"], fixture.Events);
        Assert.Equal(2, fixture.Runner.RunCount);
        Assert.Equal(1, fixture.Primary.LaunchCount);
        Assert.Equal(1, fixture.Auxiliary.LaunchCount);
        Assert.Equal(1, fixture.Storage.PublicationCount);
        Assert.All(result.Workloads, q => Assert.Equal(EWorkerWorkloadState.Stopped, q.State));
    }

    /// <summary>Provides a real filesystem with deterministic process and content side effects.</summary>
    private sealed class Fixture : IDisposable
    {
        /// <summary>Owns the isolated temporary artifact directory.</summary>
        private readonly TemporaryDirectory _directory = new();
        /// <summary>Retains the deployment and worker identities used by every test command.</summary>
        private readonly CloudWorkerBootstrapConfig _bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        /// <summary>Provides stable local worker paths and native executable settings.</summary>
        private readonly CloudWorkerOptions _options;
        /// <summary>Generates monotonically ordered durable commands.</summary>
        private long _sequence;

        /// <summary>Creates fixture-owned game artifacts and injectable dependencies.</summary>
        /// <param name="joinTimeout">An optional bounded timeout for engine-readiness tests.</param>
        public Fixture(TimeSpan? joinTimeout = null)
        {
            var steam = Path.Combine(_directory.Path, "steamcmd");
            File.WriteAllText(steam, "placeholder");
            _options = new CloudWorkerOptions(Path.Combine(_directory.Path, "bootstrap.json"),
                Path.Combine(_directory.Path, "state"), Path.Combine(_directory.Path, "runtime"),
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15), true, false,
                new Arma3WorkerOptions(steam, "anonymous", null, null, "arma3server_x64", TimeSpan.FromSeconds(30))
                {
                    TemporaryDirectory = Path.Combine(_directory.Path, "tmp"),
                    HeadlessClientJoinTimeout = joinTimeout ?? TimeSpan.FromMinutes(3),
                });
            Paths = new Arma3RuntimePaths(_options.RuntimeRoot, _options.StateDirectory, _bootstrap.DeploymentId,
                _options.Arma3.ExecutableName);
            AuxiliaryPaths = new Arma3RuntimePaths(_options.RuntimeRoot, _options.StateDirectory, _bootstrap.DeploymentId,
                _options.Arma3.ExecutableName, "headless-client", 0);
            Primary = new ProcessController(_bootstrap, Paths, Events, 1000);
            Auxiliary = new ProcessController(_bootstrap, AuxiliaryPaths, Events, 2000);
            Runner = new ContentRunner(Paths, Events, Primary, Auxiliary);
            Storage = new StorageTransfer(Paths, Events, Primary, Auxiliary);
            Executor = CreateExecutor();
        }

        /// <summary>Gets observed side effects in invocation order.</summary>
        /// <value>The ordered event log.</value>
        public List<string> Events { get; } = [];
        /// <summary>Gets the primary role's filesystem locations.</summary>
        /// <value>The primary runtime paths.</value>
        public Arma3RuntimePaths Paths { get; }
        /// <summary>Gets separate mutable paths sharing the primary installation.</summary>
        /// <value>The dependent runtime paths.</value>
        public Arma3RuntimePaths AuxiliaryPaths { get; }
        /// <summary>Gets the primary process simulator.</summary>
        /// <value>The primary controller.</value>
        public ProcessController Primary { get; }
        /// <summary>Gets the dependent process simulator.</summary>
        /// <value>The dependent controller.</value>
        public ProcessController Auxiliary { get; }
        /// <summary>Gets the content preparation simulator.</summary>
        /// <value>The Steam command runner.</value>
        public ContentRunner Runner { get; }
        /// <summary>Gets the persistent-state simulator.</summary>
        /// <value>The storage adapter.</value>
        public StorageTransfer Storage { get; }
        /// <summary>Gets the initial worker executor.</summary>
        /// <value>The production executor with simulated native dependencies.</value>
        public Arma3WorkerCommandExecutor Executor { get; }

        /// <summary>Reconstructs the worker while preserving filesystem and process reality.</summary>
        /// <returns>A new production executor over the same attributed deployment.</returns>
        public Arma3WorkerCommandExecutor CreateExecutor()
            => new(_bootstrap.ToConnectionConfig(), _options, commandRunner: Runner,
                processController: Primary, storageTransfer: Storage, auxiliaryProcessController: Auxiliary);

        /// <summary>Models a crash boundary by retaining the requested last durable supervision phase.</summary>
        /// <param name="phase">The phase that was durably recorded before the crash.</param>
        /// <returns>The asynchronous journal rewrite.</returns>
        public async Task SetPhaseAsync(string phase)
        {
            var path = Path.Combine(Paths.AgentStatePath, "arma3-supervision.json");
            var state = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            state["phase"] = phase;
            await File.WriteAllTextAsync(path, state.ToJsonString(CloudWorkerJson.Options));
        }

        /// <summary>Creates an immutable command assigning both roles to one host.</summary>
        /// <param name="action">The requested lifecycle operation.</param>
        /// <returns>The serialized command envelope with role-specific selected content.</returns>
        public WorkerCommandClaimResponse Command(string action)
        {
            var command = FlatFileWorkerCommandJournalTests.CreateCommand(_bootstrap.WorkerId,
                _bootstrap.DeploymentId, _bootstrap.ServerId, action);
            var payload = JsonSerializer.Deserialize<WorkerCommandPayload>(command.Payload, CloudWorkerJson.Options)!;
            payload = payload with
            {
                Port = 2302,
                Topology = [new WorkerNodeEndpoint
                {
                    ServiceIdentifier = "game-server", IsPrimary = true, Address = "192.0.2.10", Port = 2302,
                }],
                Workloads =
                [
                    new WorkerWorkloadAssignment
                    {
                        ServiceIdentifier = "game-server", HostServiceIdentifier = "game-server", IsPrimary = true,
                    },
                    new WorkerWorkloadAssignment
                    {
                        ServiceIdentifier = "headless-client", HostServiceIdentifier = "game-server",
                    },
                ],
                Content = [Content("101", "Shared"), Content("102", "ServerOnly"), Content("103", "ClientOnly")],
            };
            return command with { Sequence = ++_sequence, Payload = JsonSerializer.Serialize(payload, CloudWorkerJson.Options) };
        }

        /// <summary>Creates an allowlisted Workshop selection.</summary>
        /// <param name="id">The Workshop identifier.</param>
        /// <param name="role">The workload applicability.</param>
        /// <returns>The role-scoped content reference.</returns>
        private static WorkerContentReference Content(string id, string role)
            => new() { Source = "steam-workshop", Identifier = id, DisplayName = "Mod" + id, UsageRole = role };

        /// <inheritdoc />
        public void Dispose() => _directory.Dispose();
    }

    /// <summary>Models exact process identities and records lifecycle side effects.</summary>
    /// <param name="bootstrap">The deployment attribution.</param>
    /// <param name="paths">The role's mutable paths.</param>
    /// <param name="events">The shared event log.</param>
    /// <param name="pidBase">The distinct role-specific PID range.</param>
    private sealed class ProcessController(CloudWorkerBootstrapConfig bootstrap, Arma3RuntimePaths paths,
        List<string> events, int pidBase) : IArma3ProcessController
    {
        /// <summary>Gets the currently attributed process.</summary>
        /// <value>The current incarnation, or null after shutdown.</value>
        public Arma3ProcessIdentity? Identity { get; private set; }
        /// <summary>Gets whether the simulated native process exists.</summary>
        /// <value>True while an attributed process remains active.</value>
        public bool Running => Identity is not null;
        /// <summary>Gets the number of new native process launches.</summary>
        /// <value>The successful spawn count.</value>
        public int LaunchCount { get; private set; }
        /// <summary>Gets the last role-specific command line.</summary>
        /// <value>The copied launch arguments.</value>
        public IReadOnlyList<string> Arguments { get; private set; } = [];
        /// <summary>Gets the configuration seen at launch.</summary>
        /// <value>The primary file contents, when present.</value>
        public string ConfigurationAtStart { get; private set; } = string.Empty;
        /// <summary>Gets or sets whether the next launch is rejected.</summary>
        /// <value>True to simulate process creation failure.</value>
        public bool FailStart { get; set; }
        /// <summary>Gets or sets whether graceful shutdown reports failure.</summary>
        /// <value>True to leave the process alive during Stop.</value>
        public bool FailStop { get; set; }
        /// <summary>Gets or sets whether graceful shutdown throws an I/O failure.</summary>
        /// <value>True to test independent shutdown attempts.</value>
        public bool ThrowOnStop { get; set; }

        /// <inheritdoc />
        public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Arma3ProcessObservation(Running, Identity, null, null));

        /// <inheritdoc />
        public Task<WorkerCommandExecutionResult> StartAsync(IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailStart)
                return Task.FromResult(WorkerCommandExecutionResult.Failure("start-failed", "spawn-failed", "Simulated spawn failure."));
            if (!Running)
            {
                events.Add("start-" + paths.ServiceIdentifier);
                LaunchCount++;
                Identity = new Arma3ProcessIdentity(bootstrap.ServerId, bootstrap.DeploymentId,
                    pidBase + LaunchCount, DateTimeOffset.UtcNow, paths.ExecutablePath, paths.ServiceIdentifier);
                Arguments = arguments.ToArray();
                ConfigurationAtStart = File.Exists(paths.ServerConfigurationPath)
                    ? File.ReadAllText(paths.ServerConfigurationPath) : string.Empty;
            }
            return Task.FromResult(WorkerCommandExecutionResult.Success("started", processId: Identity!.ProcessId,
                processStartedAt: Identity.ProcessStartedAt));
        }

        /// <inheritdoc />
        public Task<WorkerCommandExecutionResult> StopAsync(bool force, TimeSpan timeout, int gracefulAttempts,
            CancellationToken cancellationToken)
        {
            events.Add("stop-" + paths.ServiceIdentifier);
            if (ThrowOnStop)
                throw new IOException("Simulated stop failure.");
            if (FailStop)
                return Task.FromResult(WorkerCommandExecutionResult.Failure("stop-failed", "process-alive", "Process remains alive."));
            Identity = null;
            return Task.FromResult(WorkerCommandExecutionResult.Success("stopped"));
        }

        /// <inheritdoc />
        public Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
        {
            events.Add("terminate-" + paths.ServiceIdentifier);
            Identity = null;
            return Task.FromResult(WorkerCommandExecutionResult.Success("force-stopped"));
        }
    }

    /// <summary>Materializes only the Workshop items requested in the actual Steam script.</summary>
    /// <param name="paths">Shared installation paths.</param>
    /// <param name="events">The shared event log.</param>
    /// <param name="primary">The primary writer.</param>
    /// <param name="auxiliary">The dependent writer.</param>
    private sealed class ContentRunner(Arma3RuntimePaths paths, List<string> events,
        ProcessController primary, ProcessController auxiliary) : IProcessCommandRunner
    {
        /// <summary>Gets the number of Steam preparations.</summary>
        /// <value>The invocation count.</value>
        public int RunCount { get; private set; }
        /// <summary>Gets the submitted Steam script.</summary>
        /// <value>The last preparation instructions.</value>
        public string Script { get; private set; } = string.Empty;
        /// <summary>Gets or sets an optional asynchronous boundary before shared installation is modified.</summary>
        /// <value>A callback that pauses preparation while tests observe the stopped process group.</value>
        public Func<CancellationToken, Task>? BeforePreparation { get; set; }

        /// <inheritdoc />
        public async Task<ProcessCommandResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            Assert.False(primary.Running);
            Assert.False(auxiliary.Running);
            events.Add("prepare");
            RunCount++;
            if (BeforePreparation is not null)
                await BeforePreparation(cancellationToken);
            Script = await File.ReadAllTextAsync(startInfo.ArgumentList.Last(), cancellationToken);
            Directory.CreateDirectory(paths.InstallPath);
            await File.WriteAllTextAsync(paths.ExecutablePath, "prepared", cancellationToken);
            foreach (Match match in Regex.Matches(Script, @"workshop_download_item 107410 (\d+)"))
                Directory.CreateDirectory(paths.GetSteamWorkshopItemPath(ulong.Parse(match.Groups[1].Value,
                    CultureInfo.InvariantCulture)));
            return new ProcessCommandResult(true, 0, "prepared", string.Empty);
        }
    }

    /// <summary>Restores stale admission and verifies preservation runs only after both writers stop.</summary>
    /// <param name="paths">The primary configuration paths.</param>
    /// <param name="events">The shared event log.</param>
    /// <param name="primary">The primary writer.</param>
    /// <param name="auxiliary">The dependent writer.</param>
    private sealed class StorageTransfer(Arma3RuntimePaths paths, List<string> events,
        ProcessController primary, ProcessController auxiliary) : ICloudWorkerStorageTransfer
    {
        /// <summary>Gets the number of committed preservation operations.</summary>
        /// <value>The publication count.</value>
        public int PublicationCount { get; private set; }

        /// <inheritdoc />
        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task ApplyPersistentSnapshotAsync(WorkerCommandClaimResponse command, string deploymentRoot,
            CancellationToken cancellationToken)
        {
            events.Add("restore");
            return File.WriteAllTextAsync(paths.ServerConfigurationPath,
                "hostname = \"restored\";\nheadlessClients[] = {\"192.0.2.99\"};\nlocalClient[] = {\"192.0.2.99\"};\n",
                cancellationToken);
        }

        /// <inheritdoc />
        public Task PublishPreservationAsync(WorkerCommandClaimResponse command, string deploymentRoot,
            CancellationToken cancellationToken)
        {
            Assert.False(primary.Running);
            Assert.False(auxiliary.Running);
            events.Add("publish");
            PublicationCount++;
            return Task.CompletedTask;
        }
    }
}
