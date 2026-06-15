using System.Net;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class CloudWorkerRuntimeTests
{
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
        var api = new FakeApiClient();
        var runtime = new CloudWorkerRuntime(
            api,
            credentialStore,
            new FlatFileWorkerCommandJournal(directory.Path),
            new FakeExecutor());
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "deleted-bootstrap.json"),
            directory.Path,
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                "unused-steamcmd",
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30)));

        var exitCode = await runtime.RunAsync(connection, null, options, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, api.HeartbeatCount);
        Assert.Equal(1, api.ClaimCount);
        Assert.Equal("persisted-worker-token", api.LastCredential);
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
        var api = new FakeApiClient
        {
            RotationResult = new WorkerCredentialRotationResult(
                "replacement-worker-token",
                new WorkerCredentialRotationResponse
                {
                    WorkerId = connection.WorkerId,
                    DeploymentId = connection.DeploymentId,
                    CredentialExpiresAt = now.AddHours(2),
                }),
        };
        var runtime = new CloudWorkerRuntime(
            api,
            credentialStore,
            new FlatFileWorkerCommandJournal(directory.Path),
            new FakeExecutor());
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "bootstrap.json"),
            directory.Path,
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                "unused-steamcmd",
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30)));

        var exitCode = await runtime.RunAsync(connection, null, options, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, api.RotationCount);
        Assert.Equal("current-worker-token", api.LastRotationCredential);
        Assert.Equal("replacement-worker-token", api.LastCredential);
        Assert.Equal("replacement-worker-token", (await credentialStore.ReadAsync())?.Credential);
    }

    [Fact]
    public async Task NewCommand_PersistsEveryStateBeforeReportingTerminalOutcome()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var api = new FakeApiClient();
        var executor = new FakeExecutor
        {
            ExecutionResult = WorkerCommandExecutionResult.Success(
                "started",
                output: "collected-log-output",
                processId: 8123,
                processStartedAt: DateTimeOffset.UtcNow),
        };
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(EWorkerCommandJournalState.Succeeded, entry?.State);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal("collected-log-output", api.Reports[^1].Request.Output);
        Assert.Equal(
            [
                EWorkerCommandReportState.Received,
                EWorkerCommandReportState.Running,
                EWorkerCommandReportState.Succeeded,
            ],
            api.Reports.Select(q => q.Request.State).ToArray());
    }

    [Fact]
    public async Task TerminalRedelivery_ReportsRecordedOutcomeWithoutExecutingAgain()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        await journal.RecordSucceededAsync(
            command,
            WorkerCommandExecutionResult.Success(
                "already-started",
                processId: 9,
                processStartedAt: DateTimeOffset.UtcNow));
        var api = new FakeApiClient();
        var executor = new FakeExecutor();
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, executor.ReconcileCount);
        Assert.Single(api.Reports);
        Assert.Equal(EWorkerCommandReportState.Succeeded, api.Reports[0].Request.State);
        Assert.Equal(9, api.Reports[0].Request.ProcessId);
    }

    [Fact]
    public async Task RunningRedelivery_ReconcilesRealityBeforeRepeatingSideEffect()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        await journal.RecordReceivedAsync(command);
        await journal.RecordRunningAsync(command, "starting");
        var api = new FakeApiClient();
        var executor = new FakeExecutor
        {
            ReconciliationResult = WorkerCommandExecutionResult.Success(
                "reconciled-running",
                processId: 77,
                processStartedAt: DateTimeOffset.UtcNow),
        };
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.Equal(1, executor.ReconcileCount);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(EWorkerCommandJournalState.Succeeded, (await journal.ReadAsync(command.CommandId))?.State);
        Assert.Equal(EWorkerCommandReportState.Succeeded, Assert.Single(api.Reports).Request.State);
    }

    [Fact]
    public async Task ExpiredNewCommand_IsJournaledFailedWithoutExecution()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId) with
        {
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var api = new FakeApiClient();
        var executor = new FakeExecutor();
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(EWorkerCommandJournalState.Failed, (await journal.ReadAsync(command.CommandId))?.State);
        Assert.Equal("command-lease-expired", Assert.Single(api.Reports).Request.ErrorCode);
    }

    [Fact]
    public async Task TransientHeartbeatFailure_DoesNotAbandonLocalExecution()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var api = new FakeApiClient
        {
            HeartbeatException = new HttpRequestException(
                "temporary",
                null,
                HttpStatusCode.ServiceUnavailable),
        };
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMilliseconds(30) };
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromMilliseconds(5),
            CancellationToken.None);

        Assert.Equal(1, executor.ExecuteCount);
        Assert.True(api.HeartbeatCount > 0);
        Assert.Equal(EWorkerCommandJournalState.Succeeded, (await journal.ReadAsync(command.CommandId))?.State);
        Assert.Equal(EWorkerCommandReportState.Succeeded, api.Reports[^1].Request.State);
    }

    [Fact]
    public async Task ControlPlaneSupersession_CancelsInFlightCommandAndPersistsBarrierOutcome()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var api = new FakeApiClient { SupersedeOnRunningReport = 2 };
        var executor = new FakeExecutor { ExecutionDelay = TimeSpan.FromMinutes(1) };
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromMilliseconds(5),
            CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.True(executor.CancellationObserved);
        Assert.Equal(EWorkerCommandJournalState.Failed, entry?.State);
        Assert.Equal("command-superseded", entry?.Outcome?.ErrorCode);
        Assert.Equal("cancelled-by-cost-stop", entry?.Stage);
    }

    [Fact]
    public async Task FastCompletedCommand_TerminalRejectionCompensatesBeforeReplacingSucceededOutcome()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId,
            "start");
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var api = new FakeApiClient { SupersedeTerminalReports = true };
        var executor = new FakeExecutor
        {
            ExecutionResult = WorkerCommandExecutionResult.Success(
                "started",
                processId: 6123,
                processStartedAt: DateTimeOffset.UtcNow),
        };
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal(1, executor.CompensationCount);
        Assert.Equal(EWorkerCommandJournalState.Failed, entry?.State);
        Assert.Equal("command-superseded", entry?.Outcome?.ErrorCode);
        Assert.Contains(api.Reports, q => q.Request.State is EWorkerCommandReportState.Succeeded);
    }

    [Fact]
    public async Task TerminalRedelivery_RejectedByBarrierCompensatesAndOverridesLocalSuccess()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId,
            "start");
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        await journal.RecordSucceededAsync(
            command,
            WorkerCommandExecutionResult.Success(
                "started",
                processId: 7123,
                processStartedAt: DateTimeOffset.UtcNow));
        var api = new FakeApiClient { SupersedeTerminalReports = true };
        var executor = new FakeExecutor();
        var runtime = new CloudWorkerRuntime(
            api,
            new CloudWorkerCredentialStore(directory.Path),
            journal,
            executor);

        await runtime.ProcessCommandAsync(
            bootstrap,
            "worker-token",
            command,
            TimeSpan.FromHours(1),
            CancellationToken.None);

        var entry = await journal.ReadAsync(command.CommandId);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(1, executor.CompensationCount);
        Assert.Equal(EWorkerCommandJournalState.Failed, entry?.State);
        Assert.Equal("command-superseded", entry?.Outcome?.ErrorCode);
    }

    private sealed class FakeExecutor : IWorkerCommandExecutor
    {
        public int ExecuteCount { get; private set; }
        public int ReconcileCount { get; private set; }
        public int CompensationCount { get; private set; }
        public WorkerCommandExecutionResult ExecutionResult { get; init; }
            = WorkerCommandExecutionResult.Success("completed");
        public WorkerCommandExecutionResult? ReconciliationResult { get; init; }
        public TimeSpan ExecutionDelay { get; init; }
        public bool CancellationObserved { get; private set; }

        public async Task<WorkerCommandExecutionResult> ExecuteAsync(
            WorkerCommandClaimResponse command,
            CancellationToken cancellationToken)
        {
            ExecuteCount++;
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
            CompensationCount++;
            return Task.CompletedTask;
        }

        public Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken)
            => Task.FromResult<WorkerRuntimeStatus?>(null);
    }

    private sealed class FakeApiClient : ICloudWorkerApiClient
    {
        public List<(Guid CommandId, WorkerCommandReportRequest Request)> Reports { get; } = [];
        public int HeartbeatCount { get; private set; }
        public int ClaimCount { get; private set; }
        public string? LastCredential { get; private set; }
        public string? LastRotationCredential { get; private set; }
        public int RotationCount { get; private set; }
        public WorkerCredentialRotationResult? RotationResult { get; init; }
        public HttpRequestException? HeartbeatException { get; init; }
        public int SupersedeOnRunningReport { get; init; } = int.MaxValue;
        public bool SupersedeTerminalReports { get; init; }
        private int RunningReportCount { get; set; }

        public Task<WorkerRegistrationResult> RegisterAsync(
            CloudWorkerBootstrapConfig bootstrap,
            WorkerRegistrationRequest request,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

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
        {
            HeartbeatCount++;
            LastCredential = workerCredential;
            if (HeartbeatException is not null)
                throw HeartbeatException;
            return Task.CompletedTask;
        }

        public Task<WorkerCommandClaimResponse?> ClaimNextAsync(
            CloudWorkerConnectionConfig connection,
            string workerCredential,
            CancellationToken cancellationToken)
        {
            ClaimCount++;
            LastCredential = workerCredential;
            return Task.FromResult<WorkerCommandClaimResponse?>(null);
        }

        public Task<bool> ReportCommandAsync(
            CloudWorkerConnectionConfig connection,
            string workerCredential,
            Guid commandId,
            WorkerCommandReportRequest request,
            CancellationToken cancellationToken)
        {
            Reports.Add((commandId, request));
            if (request.State is EWorkerCommandReportState.Running)
                RunningReportCount++;
            var terminalSuperseded = SupersedeTerminalReports
                                     && request.State is EWorkerCommandReportState.Succeeded
                                         or EWorkerCommandReportState.Failed;
            return Task.FromResult(
                !terminalSuperseded && RunningReportCount < SupersedeOnRunningReport);
        }
    }
}
