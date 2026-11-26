using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class CloudWorkerRuntimeTests
{
    private static readonly string[] AllGames = ["arma3", "aska", "cs2"];

    [Fact]
    public async Task NewRegistration_AdvertisesProtocolThreeAndEveryImplementedGame()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var connection = bootstrap.ToConnectionConfig();
        var api = new FakeApiClient();
        var channel = new FakeControlChannel();
        var runtime = CreateRuntime(api, channel, directory, new FakeExecutor());

        var exitCode = await runtime.RunAsync(
            connection,
            bootstrap,
            CreateOptions(directory, true),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(WorkerChannelProtocol.ProtocolVersion, api.LastRegistrationRequest?.ProtocolVersion);
        Assert.Equal(3, api.LastRegistrationRequest?.ProtocolVersion);
        Assert.Equal(AllGames, api.LastRegistrationRequest?.SupportedGameIdentifiers.Order());
        Assert.Equal(
            ["git", "steam-workshop", "uploaded-artifact"],
            api.LastRegistrationRequest?.SupportedContentProviderIdentifiers.Order());
        Assert.Equal(["registered-worker-token"], channel.ConnectCredentials);
        Assert.Equal(connection, channel.StartedConnection);
    }

    [Fact]
    public async Task FirstSuccessfulHeartbeat_RetiresTheBootstrapFile()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var options = CreateOptions(directory, true);
        await File.WriteAllTextAsync(options.BootstrapPath, "{}");
        var runtime = CreateRuntime(new FakeApiClient(), new FakeControlChannel(), directory, new FakeExecutor());

        await runtime.RunAsync(bootstrap.ToConnectionConfig(), bootstrap, options, CancellationToken.None);

        Assert.False(File.Exists(options.BootstrapPath));
    }

    [Fact]
    public async Task PersistedRegistration_RestartsWithoutBootstrapFile()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var credentialStore = new CloudWorkerCredentialStore(directory.Path);
        await credentialStore.SaveAsync(new StoredWorkerCredential(
            connection,
            "persisted-worker-token",
            DateTimeOffset.UtcNow.AddHours(1),
            9,
            DateTimeOffset.UtcNow));
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor
        {
            RuntimeStatus = new WorkerRuntimeStatus(
                "prepared",
                null,
                null,
                null,
                10_000,
                4_000)
            {
                Workloads =
                [
                    new WorkerWorkloadObservation
                    {
                        ServiceIdentifier = "headless-client",
                        State = EWorkerWorkloadState.Stopped,
                        ObservedAt = DateTimeOffset.UtcNow,
                    },
                ],
            },
        };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);

        var exitCode = await runtime.RunAsync(
            connection,
            null,
            CreateOptions(directory, true, false),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, channel.HeartbeatCount);
        Assert.Equal(1, channel.ClaimCount);
        Assert.Equal(["persisted-worker-token"], channel.ConnectCredentials);
        var heartbeat = Assert.Single(channel.Heartbeats);
        Assert.Equal("idle", heartbeat.Stage);
        Assert.Equal(connection.DeploymentId, heartbeat.DeploymentId);
        Assert.Equal(10_000, heartbeat.DeploymentStorageTotalBytes);
        Assert.Equal(4_000, heartbeat.DeploymentStorageAvailableBytes);
        Assert.Equal(executor.RuntimeStatus.Workloads, heartbeat.Workloads);
    }

    [Fact]
    public async Task ExpiringCredential_IsPersistedAtomicallyBeforeReplacementIsUsed()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var credentialStore = new CloudWorkerCredentialStore(directory.Path);
        var now = DateTimeOffset.UtcNow;
        await credentialStore.SaveAsync(new StoredWorkerCredential(
            connection,
            "current-worker-token",
            now.AddSeconds(20),
            9,
            now.AddMinutes(-10)));
        var api = new FakeApiClient { RotationResult = CreateRotation(connection, "replacement-worker-token") };
        var channel = new FakeControlChannel();
        var runtime = CreateRuntime(api, channel, directory, new FakeExecutor());

        var exitCode = await runtime.RunAsync(
            connection,
            null,
            CreateOptions(directory, true, false),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, api.RotationCount);
        Assert.Equal("current-worker-token", api.LastRotationCredential);
        Assert.Equal(["replacement-worker-token"], channel.ConnectCredentials);
        Assert.Equal("replacement-worker-token", (await credentialStore.ReadAsync())?.Credential);
    }

    [Fact]
    public async Task RotationWhileRunning_IsSavedBeforeTheChannelReconnectsWithIt()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var credentialStore = new CloudWorkerCredentialStore(directory.Path);
        var now = DateTimeOffset.UtcNow;
        // Lifetime 8 s gives a 4 s rotation lead: not due at start, due at the first one-second heartbeat tick.
        await credentialStore.SaveAsync(new StoredWorkerCredential(
            connection,
            "current-worker-token",
            now.AddSeconds(5),
            1,
            now.AddSeconds(-3)));
        var api = new FakeApiClient { RotationResult = CreateRotation(connection, "replacement-worker-token") };
        string? storedAtReconnect = null;
        var channel = new FakeControlChannel
        {
            OnReconnect = async () => storedAtReconnect = (await credentialStore.ReadAsync())?.Credential,
        };
        var runtime = CreateRuntime(api, channel, directory, new FakeExecutor());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var run = runtime.RunAsync(connection, null, CreateOptions(directory, false, false), cancellation.Token);

        await WaitUntilAsync(() => channel.ReconnectCount >= 1);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal("replacement-worker-token", storedAtReconnect);
        Assert.Equal(["current-worker-token", "replacement-worker-token"], channel.ConnectCredentials);
        Assert.Equal(1, api.RotationCount);
    }

    [Fact]
    public async Task RejectedCredential_EndsTheWorkerWithTheAuthenticationFault()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, new FakeExecutor());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var run = runtime.RunAsync(connection, null, CreateOptions(directory, false, false), cancellation.Token);

        await WaitUntilAsync(() => channel.ClaimCount >= 1);
        channel.FaultCompletion(new WorkerChannelAuthenticationException("rejected"));

        await Assert.ThrowsAsync<WorkerChannelAuthenticationException>(() => run);
    }

    [Fact]
    public async Task IdleWorker_SendsNoClaimUntilAWakeArrives()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, new FakeExecutor());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var run = runtime.RunAsync(connection, null, CreateOptions(directory, false, false), cancellation.Token);

        await WaitUntilAsync(() => channel.ClaimCount >= 1);
        await Task.Delay(500);
        Assert.Equal(1, channel.ClaimCount);

        channel.PushWake();
        await WaitUntilAsync(() => channel.ClaimCount >= 2);
        await Task.Delay(300);
        Assert.Equal(2, channel.ClaimCount);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task ClaimedCommand_IsFollowedByAnImmediateClaim()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var connection = bootstrap.ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        channel.EnqueueClaim(CreateCommand(bootstrap));
        var executor = new FakeExecutor();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var run = runtime.RunAsync(connection, null, CreateOptions(directory, false, false), cancellation.Token);

        await WaitUntilAsync(() => channel.ClaimCount >= 2);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal(EWorkerCommandReportState.Succeeded, channel.Reports[^1].Request.State);
    }

    [Fact]
    public async Task NewCommand_PersistsEveryStateBeforeReportingIt()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var executor = new FakeExecutor
        {
            ExecutionResult = WorkerCommandExecutionResult.Success(
                "started",
                output: "collected-log-output",
                processId: 8123,
                processStartedAt: DateTimeOffset.UtcNow) with
            {
                Workloads =
                [
                    new WorkerWorkloadObservation
                    {
                        ServiceIdentifier = "headless-client",
                        ProcessId = 8124,
                        ProcessStartedAt = DateTimeOffset.UtcNow,
                        State = EWorkerWorkloadState.Running,
                        ObservedAt = DateTimeOffset.UtcNow,
                    },
                ],
            },
        };
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var journalStateAtReport = new List<EWorkerCommandJournalState?>();
        var channel = new FakeControlChannel
        {
            OnReport = async _ => journalStateAtReport.Add((await journal.ReadAsync(command.CommandId))?.State),
        };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(EWorkerCommandJournalState.Succeeded, entry?.State);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal("collected-log-output", channel.Reports[^1].Request.Output);
        Assert.Equal(executor.ExecutionResult.Workloads, channel.Reports[^1].Request.Workloads);
        Assert.Equal(executor.ExecutionResult.Workloads, entry?.Outcome?.Workloads);
        Assert.Equal(
            [
                EWorkerCommandReportState.Received,
                EWorkerCommandReportState.Running,
                EWorkerCommandReportState.Succeeded,
            ],
            channel.Reports.Select(q => q.Request.State).ToArray());
        Assert.Equal(
            [
                EWorkerCommandJournalState.Received,
                EWorkerCommandJournalState.Running,
                EWorkerCommandJournalState.Succeeded,
            ],
            journalStateAtReport);
    }

    [Fact]
    public async Task LargeOutput_ReachesTheChannelBoundedByTheJournal()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var output = new string('x', 400_000);
        var executor = new FakeExecutor { ExecutionResult = WorkerCommandExecutionResult.Success("started", output) };
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var channel = new FakeControlChannel();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        var reported = channel.Reports[^1].Request.Output;
        Assert.NotNull(reported);
        // The journal bounds the outcome before it is reported, so no report can exceed the 256 KiB channel frame
        // limit that makes the control plane close the channel with 1009.
        Assert.True(reported.Length <= 16384);
        Assert.True((await journal.ReadAsync(command.CommandId))?.Outcome?.Output?.Length <= 16384);
    }

    [Fact]
    public async Task LargeFailure_ReachesTheChannelWithBoundedOutputAndErrorMessage()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var executor = new FakeExecutor
        {
            ExecutionResult = WorkerCommandExecutionResult.Failure("failed", "boom", new string('e', 50_000))
                with
                {
                    Output = new string('o', 300_000),
                },
        };
        var channel = new FakeControlChannel();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        var request = channel.Reports[^1].Request;
        Assert.Equal(EWorkerCommandReportState.Failed, request.State);
        Assert.True(request.Output?.Length <= 16384);
        Assert.True(request.ErrorMessage?.Length <= 2048);
    }

    [Fact]
    public async Task TerminalRedelivery_ReportsRecordedOutcomeWithoutExecutingAgain()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var workload = new WorkerWorkloadObservation
        {
            ServiceIdentifier = "headless-client",
            ProcessId = 10,
            ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            State = EWorkerWorkloadState.Running,
            ObservedAt = DateTimeOffset.UtcNow,
        };
        await journal.RecordSucceededAsync(
            command,
            WorkerCommandExecutionResult.Success(
                "already-started",
                processId: 9,
                processStartedAt: DateTimeOffset.UtcNow) with
            {
                Workloads = [workload],
            });
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, executor.ReconcileCount);
        var report = Assert.Single(channel.Reports);
        Assert.Equal(EWorkerCommandReportState.Succeeded, report.Request.State);
        Assert.Equal(9, report.Request.ProcessId);
        Assert.Equal(workload, Assert.Single(report.Request.Workloads));
    }

    [Fact]
    public async Task RunningRedelivery_ReconcilesRealityBeforeRepeatingSideEffect()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        await journal.RecordReceivedAsync(command);
        await journal.RecordRunningAsync(command, "starting");
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor
        {
            ReconciliationResult = WorkerCommandExecutionResult.Success(
                "reconciled-running",
                processId: 77,
                processStartedAt: DateTimeOffset.UtcNow),
        };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        Assert.Equal(1, executor.ReconcileCount);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(EWorkerCommandJournalState.Succeeded, (await journal.ReadAsync(command.CommandId))?.State);
        Assert.Equal(EWorkerCommandReportState.Succeeded, Assert.Single(channel.Reports).Request.State);
    }

    [Fact]
    public async Task ExpiredNewCommand_IsJournaledFailedWithoutExecution()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap) with { LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(EWorkerCommandJournalState.Failed, (await journal.ReadAsync(command.CommandId))?.State);
        Assert.Equal("command-lease-expired", Assert.Single(channel.Reports).Request.ErrorCode);
    }

    [Fact]
    public async Task HeartbeatsContinueAtTheirOwnIntervalWhileACommandExecutes()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var connection = bootstrap.ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 1);
        var channel = new FakeControlChannel();
        channel.EnqueueClaim(CreateCommand(bootstrap));
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMilliseconds(3500) };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);

        var exitCode = await runtime.RunAsync(
            connection,
            null,
            CreateOptions(directory, true, false),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal("idle", channel.Heartbeats[0].Stage);
        Assert.True(channel.Heartbeats.Count(q => q.Stage == "running") >= 2);
        Assert.Equal(EWorkerCommandReportState.Succeeded, channel.Reports[^1].Request.State);
    }

    [Fact]
    public async Task TransientHeartbeatFailure_DoesNotAbandonLocalExecution()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var connection = bootstrap.ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 1);
        var command = CreateCommand(bootstrap);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var channel = new FakeControlChannel { FailHeartbeatsFrom = 2 };
        channel.EnqueueClaim(command);
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMilliseconds(2500) };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        var exitCode = await runtime.RunAsync(
            connection,
            null,
            CreateOptions(directory, true, false),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.True(channel.HeartbeatCount >= 2);
        Assert.Equal(EWorkerCommandJournalState.Succeeded, (await journal.ReadAsync(command.CommandId))?.State);
        Assert.Equal(EWorkerCommandReportState.Succeeded, channel.Reports[^1].Request.State);
    }

    [Fact]
    public async Task RunningCommand_WithoutAWake_SendsNoProbe()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMilliseconds(400) };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        Assert.Equal(1, channel.RunningReportCount);
    }

    [Fact]
    public async Task WakeDuringExecution_ProbesAndSupersessionCancelsAndPersistsBarrierOutcome()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var channel = new FakeControlChannel { SupersedeOnRunningReport = 2 };
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMinutes(1) };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var process = runtime.ProcessCommandAsync(bootstrap, command, cancellation.Token);

        await WaitUntilAsync(() => executor.ExecuteCount == 1);
        channel.PushWake();
        await process;

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(2, channel.RunningReportCount);
        Assert.True(executor.CancellationObserved);
        Assert.Equal(EWorkerCommandJournalState.Failed, entry?.State);
        Assert.Equal("command-superseded", entry?.Outcome?.ErrorCode);
        Assert.Equal("cancelled-by-cost-stop", entry?.Stage);
        Assert.Equal(1, executor.CompensationCount);
    }

    [Fact]
    public async Task WakeDuringExecution_WithAcceptedProbe_KeepsExecuting()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMilliseconds(1500) };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);
        var process = runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        await WaitUntilAsync(() => executor.ExecuteCount == 1);
        channel.PushWake();
        await process;

        Assert.False(executor.CancellationObserved);
        Assert.Equal(2, channel.RunningReportCount);
        Assert.Equal(EWorkerCommandReportState.Succeeded, channel.Reports[^1].Request.State);
    }

    [Fact]
    public async Task FastCompletedCommand_TerminalRejectionCompensatesBeforeReplacingSucceededOutcome()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var channel = new FakeControlChannel { SupersedeTerminalReports = true };
        var executor = new FakeExecutor
        {
            ExecutionResult = WorkerCommandExecutionResult.Success(
                "started",
                processId: 6123,
                processStartedAt: DateTimeOffset.UtcNow),
        };
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal(1, executor.CompensationCount);
        Assert.Equal(EWorkerCommandJournalState.Failed, entry?.State);
        Assert.Equal("command-superseded", entry?.Outcome?.ErrorCode);
        Assert.Contains(channel.Reports, q => q.Request.State is EWorkerCommandReportState.Succeeded);
    }

    [Fact]
    public async Task TerminalRedelivery_RejectedByBarrierCompensatesAndOverridesLocalSuccess()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = CreateCommand(bootstrap);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        await journal.RecordSucceededAsync(
            command,
            WorkerCommandExecutionResult.Success(
                "started",
                processId: 7123,
                processStartedAt: DateTimeOffset.UtcNow));
        var channel = new FakeControlChannel { SupersedeTerminalReports = true };
        var executor = new FakeExecutor();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, journal);

        await runtime.ProcessCommandAsync(bootstrap, command, CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(1, executor.CompensationCount);
        Assert.Equal(EWorkerCommandJournalState.Failed, entry?.State);
        Assert.Equal("command-superseded", entry?.Outcome?.ErrorCode);
    }

    [Fact]
    public async Task TransientClaimFailure_WaitsForTheConnectionAndExecutesTheCommandOnce()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var connection = bootstrap.ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        channel.EnqueueClaimFailure(new WorkerChannelUnavailableException("down"));
        channel.EnqueueClaim(CreateCommand(bootstrap));
        var executor = new FakeExecutor();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor);

        var exitCode = await runtime.RunAsync(
            connection,
            null,
            CreateOptions(directory, true, false, TimeSpan.FromMilliseconds(20)),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, channel.ClaimCount);
        Assert.Equal(1, channel.WaitUntilConnectedCount);
        Assert.Equal(1, executor.ExecuteCount);
    }

    [Fact]
    public async Task PrimaryWorker_SendsAValidatedSampleEveryMetricsInterval()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor { Players = WorkerPlayerObservation.Players(3, 16) };
        var sampler = new FakeSampler { Reading = new WorkerMachineReading(42.5, null, 1_000, 4_000, null) };

        await RunMeteredAsync(directory, connection, channel, executor, sampler, () => channel.Metrics.Count >= 3);

        var samples = channel.Metrics.ToArray();
        Assert.All(samples, sample =>
        {
            Assert.Equal(connection.DeploymentId, sample.DeploymentId);
            Assert.True(DateTimeOffset.UtcNow - sample.SampledAt < TimeSpan.FromMinutes(1));
            Assert.Equal(42.5, sample.CpuPercent);
            Assert.Equal(1_000, sample.MemoryUsedBytes);
            Assert.Equal(4_000, sample.MemoryTotalBytes);
            Assert.Equal(3, sample.PlayerCount);
            Assert.Equal(16, sample.PlayerCapacity);
            Assert.Null(sample.CpuUnavailableReason);
            Assert.Null(sample.MemoryUnavailableReason);
            Assert.Null(sample.PlayersUnavailableReason);
            AssertValid(sample);
        });
        Assert.Equal(samples.Length, samples.Select(q => q.SampledAt).Distinct().Count());
    }

    [Fact]
    public async Task UnmeasuredValues_AreSentAsReasonsAndNeverAsZero()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor
        {
            Players = WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.GameDoesNotReport),
        };
        var sampler = new FakeSampler
        {
            Reading = new WorkerMachineReading(
                null,
                WorkerMetricUnavailableReasons.Measuring,
                null,
                null,
                WorkerMetricUnavailableReasons.UnsupportedPlatform),
        };

        await RunMeteredAsync(directory, connection, channel, executor, sampler, () => channel.Metrics.Count >= 1);

        var sample = channel.Metrics[0];
        Assert.Null(sample.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, sample.CpuUnavailableReason);
        Assert.Null(sample.MemoryUsedBytes);
        Assert.Null(sample.MemoryTotalBytes);
        Assert.Equal(WorkerMetricUnavailableReasons.UnsupportedPlatform, sample.MemoryUnavailableReason);
        Assert.Null(sample.PlayerCount);
        Assert.Null(sample.PlayerCapacity);
        Assert.Equal(WorkerMetricUnavailableReasons.GameDoesNotReport, sample.PlayersUnavailableReason);
        AssertValid(sample);
    }

    [Fact]
    public async Task EmptyServer_IsSentAsCountZeroWithItsCapacity()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor { Players = WorkerPlayerObservation.Players(0, 16) };

        await RunMeteredAsync(
            directory,
            connection,
            channel,
            executor,
            new FakeSampler(),
            () => channel.Metrics.Count >= 1);

        var sample = channel.Metrics[0];
        Assert.Equal(0, sample.PlayerCount);
        Assert.Equal(16, sample.PlayerCapacity);
        Assert.Null(sample.PlayersUnavailableReason);
        AssertValid(sample);
    }

    [Fact]
    public async Task FailingPlayerQuery_IsSentAsQueryFailed()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var executor = new FakeExecutor { PlayersException = new IOException("query failed") };

        await RunMeteredAsync(
            directory,
            connection,
            channel,
            executor,
            new FakeSampler(),
            () => channel.Metrics.Count >= 1);

        var sample = channel.Metrics[0];
        Assert.Null(sample.PlayerCount);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, sample.PlayersUnavailableReason);
        AssertValid(sample);
    }

    [Fact]
    public async Task InconsistentCapacity_IsReportedAsUnknownRatherThanRejected()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        // A game that reports more players than slots would make the whole sample invalid for the API.
        var executor = new FakeExecutor { Players = WorkerPlayerObservation.Players(5, 2) };

        await RunMeteredAsync(
            directory,
            connection,
            channel,
            executor,
            new FakeSampler(),
            () => channel.Metrics.Count >= 1);

        var sample = channel.Metrics[0];
        Assert.Equal(5, sample.PlayerCount);
        Assert.Null(sample.PlayerCapacity);
        AssertValid(sample);
    }

    [Theory]
    [InlineData("headless-client", 0)]
    [InlineData("headless-client", 1)]
    [InlineData("game-server", 1)]
    public async Task NonPrimaryHostWorker_SendsNoSample(string serviceIdentifier, int serviceOrdinal)
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig() with
        {
            ServiceIdentifier = serviceIdentifier,
            ServiceOrdinal = serviceOrdinal,
        };
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel();
        var sampler = new FakeSampler();
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, new FakeExecutor(), sampler: sampler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = CreateOptions(directory, false, false) with { MetricsInterval = TimeSpan.FromMilliseconds(20) };
        var run = runtime.RunAsync(connection, null, options, cancellation.Token);

        await WaitUntilAsync(() => channel.ClaimCount >= 1);
        await Task.Delay(300);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Empty(channel.Metrics);
        Assert.Equal(0, sampler.SampleCount);
    }

    [Fact]
    public async Task SampleThatCannotBeSent_IsDroppedAndNeverResent()
    {
        using var directory = new TemporaryDirectory();
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        await SeedCredentialAsync(directory, connection, 15);
        var channel = new FakeControlChannel { FailMetricsAt = 1 };
        var executor = new FakeExecutor { Players = WorkerPlayerObservation.Players(1, 8) };

        await RunMeteredAsync(
            directory,
            connection,
            channel,
            executor,
            new FakeSampler(),
            () => channel.Metrics.Count >= 3);

        var samples = channel.Metrics.ToArray();
        // Every interval builds a fresh sample: nothing is queued, retried or replayed after the failed first one.
        Assert.Equal(samples.Length, samples.Select(q => q.SampledAt).Distinct().Count());
        Assert.True(executor.ObservePlayersCount >= samples.Length);
    }

    [Fact]
    public void MetricsInterval_DefaultsToFifteenSecondsAndReadsArgumentAndEnvironment()
    {
        var defaults = CloudWorkerOptions.FromArgs([], Environment());
        var fromArgument = CloudWorkerOptions.FromArgs(["--metrics-seconds", "5"], Environment());
        var fromEnvironment = CloudWorkerOptions.FromArgs([], Environment("UTF_WORKER_METRICS_SECONDS", "7"));
        var invalid = CloudWorkerOptions.FromArgs(["--metrics-seconds", "0"], Environment());

        Assert.Equal(TimeSpan.FromSeconds(15), defaults.MetricsInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), fromArgument.MetricsInterval);
        Assert.Equal(TimeSpan.FromSeconds(7), fromEnvironment.MetricsInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), invalid.MetricsInterval);
    }

    /// <summary>
    /// Builds an environment resolver whose ASKA tool paths are absolute on every platform, so option parsing does
    /// not depend on the Linux defaults.
    /// </summary>
    private static Func<string, string?> Environment(string? name = null, string? value = null)
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var overrides = new Dictionary<string, string>
        {
            ["UTF_ASKA_WINE_PATH"] = Path.Combine(root, "wine"),
            ["UTF_ASKA_WINEBOOT_PATH"] = Path.Combine(root, "wineboot"),
            ["UTF_ASKA_XVFB_RUN_PATH"] = Path.Combine(root, "xvfb-run"),
            ["UTF_ASKA_SETSID_PATH"] = Path.Combine(root, "setsid"),
        };
        if (name is not null && value is not null)
            overrides[name] = value;
        return key => overrides.GetValueOrDefault(key);
    }

    private static async Task RunMeteredAsync(
        TemporaryDirectory directory,
        CloudWorkerConnectionConfig connection,
        FakeControlChannel channel,
        FakeExecutor executor,
        FakeSampler sampler,
        Func<bool> enough)
    {
        var runtime = CreateRuntime(new FakeApiClient(), channel, directory, executor, sampler: sampler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = CreateOptions(directory, false, false) with { MetricsInterval = TimeSpan.FromMilliseconds(30) };
        var run = runtime.RunAsync(connection, null, options, cancellation.Token);

        await WaitUntilAsync(enough);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    private static void AssertValid(WorkerMetricsSample sample)
    {
        var results = new List<ValidationResult>();
        Assert.True(
            Validator.TryValidateObject(sample, new ValidationContext(sample), results, true),
            string.Join("; ", results.Select(q => q.ErrorMessage)));
    }

    private static CloudWorkerRuntime CreateRuntime(
        FakeApiClient api,
        FakeControlChannel channel,
        TemporaryDirectory directory,
        IWorkerCommandExecutor executor,
        FlatFileWorkerCommandJournal? journal = null,
        IWorkerMetricsSampler? sampler = null)
        => new(
            api,
            channel,
            new CloudWorkerCredentialStore(directory.Path),
            journal ?? new FlatFileWorkerCommandJournal(directory.Path),
            executor,
            sampler ?? new FakeSampler(),
            AllGames);

    private static CloudWorkerOptions CreateOptions(
        TemporaryDirectory directory,
        bool claimOnce,
        bool allowInsecureHttp = true,
        TimeSpan? pollInterval = null)
        => new(
            Path.Combine(directory.Path, "bootstrap.json"),
            directory.Path,
            Path.Combine(directory.Path, "runtime"),
            pollInterval ?? TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            claimOnce,
            allowInsecureHttp,
            new Arma3WorkerOptions(
                "unused-steamcmd",
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30)));

    private static WorkerCommandClaimResponse CreateCommand(CloudWorkerBootstrapConfig bootstrap)
        => FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);

    private static WorkerCredentialRotationResult CreateRotation(
        CloudWorkerConnectionConfig connection,
        string credential)
        => new(
            credential,
            new WorkerCredentialRotationResponse
            {
                WorkerId = connection.WorkerId,
                DeploymentId = connection.DeploymentId,
                CredentialExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            });

    private static Task SeedCredentialAsync(
        TemporaryDirectory directory,
        CloudWorkerConnectionConfig connection,
        int heartbeatSeconds)
        => new CloudWorkerCredentialStore(directory.Path).SaveAsync(new StoredWorkerCredential(
            connection,
            "persisted-worker-token",
            DateTimeOffset.UtcNow.AddHours(1),
            heartbeatSeconds,
            DateTimeOffset.UtcNow));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException("The expected condition was not reached.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeSampler : IWorkerMetricsSampler
    {
        private int _sampleCount;

        public WorkerMachineReading Reading { get; init; } = new(10, null, 1_000, 2_000, null);

        public int SampleCount => Volatile.Read(ref _sampleCount);

        public WorkerMachineReading Sample()
        {
            Interlocked.Increment(ref _sampleCount);
            return Reading;
        }
    }

    private sealed class FakeExecutor : IWorkerCommandExecutor
    {
        private int _executeCount;
        private int _compensationCount;
        private int _observePlayersCount;

        public int ExecuteCount => Volatile.Read(ref _executeCount);
        public int ReconcileCount { get; private set; }
        public int CompensationCount => Volatile.Read(ref _compensationCount);
        public WorkerCommandExecutionResult ExecutionResult { get; init; }
            = WorkerCommandExecutionResult.Success("completed");
        public WorkerCommandExecutionResult? ReconciliationResult { get; init; }
        public TimeSpan ExecutionDelay { get; init; }
        public bool CancellationObserved { get; private set; }
        public WorkerRuntimeStatus? RuntimeStatus { get; init; }
        public WorkerPlayerObservation Players { get; init; }
            = WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.GameDoesNotReport);
        public Exception? PlayersException { get; init; }
        public int ObservePlayersCount => Volatile.Read(ref _observePlayersCount);

        public Task<WorkerPlayerObservation> ObservePlayersAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _observePlayersCount);
            return PlayersException is { } exception
                ? Task.FromException<WorkerPlayerObservation>(exception)
                : Task.FromResult(Players);
        }

        public async Task<WorkerCommandExecutionResult> ExecuteAsync(
            WorkerCommandClaimResponse command,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executeCount);
            if (ExecutionDelay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(ExecutionDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CancellationObserved = true;
                    throw;
                }
            }
            return ExecutionResult;
        }

        public Task<WorkerCommandExecutionResult?> ReconcileRedeliveryAsync(
            WorkerCommandClaimResponse command,
            WorkerCommandJournalEntry journalEntry,
            CancellationToken cancellationToken)
        {
            ReconcileCount++;
            return Task.FromResult(ReconciliationResult);
        }

        public Task CompensateSupersededAsync(
            WorkerCommandClaimResponse command,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _compensationCount);
            return Task.CompletedTask;
        }

        public Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken)
            => Task.FromResult(RuntimeStatus);
    }

    private sealed class FakeApiClient : ICloudWorkerApiClient
    {
        public string? LastRotationCredential { get; private set; }
        public int RotationCount { get; private set; }
        public WorkerCredentialRotationResult? RotationResult { get; init; }
        public WorkerRegistrationRequest? LastRegistrationRequest { get; private set; }

        public Task<WorkerRegistrationResult> RegisterAsync(
            CloudWorkerBootstrapConfig bootstrap,
            WorkerRegistrationRequest request,
            CancellationToken cancellationToken)
        {
            LastRegistrationRequest = request;
            return Task.FromResult(new WorkerRegistrationResult(
                "registered-worker-token",
                new WorkerRegistrationResponse
                {
                    WorkerId = bootstrap.WorkerId,
                    DeploymentId = bootstrap.DeploymentId,
                    CredentialExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                    HeartbeatIntervalSeconds = 15,
                }));
        }

        public Task<WorkerCredentialRotationResult> RotateCredentialAsync(
            CloudWorkerConnectionConfig connection,
            string workerCredential,
            CancellationToken cancellationToken)
        {
            RotationCount++;
            LastRotationCredential = workerCredential;
            return Task.FromResult(
                RotationResult ?? throw new InvalidOperationException("Unexpected credential rotation."));
        }

        public Task HeartbeatAsync(
            CloudWorkerConnectionConfig connection,
            string workerCredential,
            WorkerHeartbeatRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The runtime must not heartbeat over HTTP.");

        public Task<WorkerCommandClaimResponse?> ClaimNextAsync(
            CloudWorkerConnectionConfig connection,
            string workerCredential,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The runtime must not claim over HTTP.");

        public Task<bool> ReportCommandAsync(
            CloudWorkerConnectionConfig connection,
            string workerCredential,
            Guid commandId,
            WorkerCommandReportRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The runtime must not report over HTTP.");
    }
}
