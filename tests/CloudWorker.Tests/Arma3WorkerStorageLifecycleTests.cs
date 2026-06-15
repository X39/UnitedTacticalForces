using System.Text.Json.Nodes;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class Arma3WorkerStorageLifecycleTests
{
    [Fact]
    public async Task Stop_PublishesPreservationOnlyAfterGracefulStop()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => processController.StopCount > 0);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);
        var command = CreateCommand(bootstrap, "stop");

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("stopped", result.Stage);
        Assert.Equal(1, processController.StopCount);
        Assert.False(processController.LastForce);
        Assert.Equal(1, storage.PublishCount);
        Assert.True(storage.PublishedAfterStop);
    }

    [Fact]
    public async Task Stop_AfterForcedFallback_PreservesPotentiallyInconsistentEvidence()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController
        {
            StopResult = WorkerCommandExecutionResult.Success(
                "force-stopped",
                "Graceful stop timed out; runtime state may be potentially inconsistent."),
        };
        var storage = new RecordingStorageTransfer(() => processController.StopCount > 0);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);

        var result = await executor.ExecuteAsync(CreateCommand(bootstrap, "stop"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("force-stopped", result.Stage);
        Assert.Contains("potentially inconsistent", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, storage.PublishCount);
    }

    [Fact]
    public async Task Stop_WhenPreservationFails_ReportsFailedCommand()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => true)
        {
            PublishException = new InvalidDataException("archive integrity failed"),
        };
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);

        var result = await executor.ExecuteAsync(CreateCommand(bootstrap, "stop"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("preservation-failed", result.Stage);
        Assert.Equal("preservation-transfer-failed", result.ErrorCode);
        Assert.Contains("ProcessStopOutcome=stopped", result.Output, StringComparison.Ordinal);
        Assert.Equal(1, processController.StopCount);
        Assert.Equal(1, storage.PublishCount);
    }

    [Fact]
    public async Task FaultStop_CollectsDiagnosticsThenStopsAndPreserves()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => processController.StopCount > 0);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);

        var result = await executor.ExecuteAsync(CreateCommand(bootstrap, "fault-stop"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("stopped", result.Stage);
        Assert.Contains("Best-effort fault diagnostics", result.Output, StringComparison.Ordinal);
        Assert.Contains("ProcessStopOutcome=stopped", result.Output, StringComparison.Ordinal);
        Assert.Equal(1, processController.StopCount);
        Assert.Equal(1, storage.PublishCount);
    }

    [Fact]
    public async Task FaultStop_ExpiredDeadlineDoesNotTouchRuntimeOrPreservation()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => true);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);
        var command = CreateCommand(bootstrap, "fault-stop");
        var payload = JsonNode.Parse(command.Payload)!.AsObject();
        payload["faultEvidenceDeadline"] = JsonValue.Create(DateTimeOffset.UtcNow.AddSeconds(-1));

        var result = await executor.ExecuteAsync(
            command with { Payload = payload.ToJsonString() },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("fault-evidence-deadline-expired", result.ErrorCode);
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);
    }

    [Fact]
    public async Task FaultStop_RedeliveryUsesPersistedDeadlineBeforeRuntimeReconciliation()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => true);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);
        var command = CreateCommand(bootstrap, "fault-stop");
        var payload = JsonNode.Parse(command.Payload)!.AsObject();
        payload["faultEvidenceDeadline"] = JsonValue.Create(DateTimeOffset.UtcNow.AddSeconds(-1));
        command = command with { Payload = payload.ToJsonString() };
        var journal = new FlatFileWorkerCommandJournal(Path.Combine(directory.Path, "redelivery-state"));
        await journal.RecordReceivedAsync(command);
        await journal.RecordRunningAsync(command, "running");
        var entry = Assert.IsType<WorkerCommandJournalEntry>(await journal.ReadAsync(command.CommandId));

        var result = await executor.ReconcileRedeliveryAsync(command, entry, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.Equal("fault-evidence-deadline-expired", result.ErrorCode);
        Assert.Equal(0, processController.ObserveCount);
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);
    }

    [Fact]
    public async Task FaultStop_DeadlineCancelsStalledPreservation()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => true)
        {
            PublishDelay = TimeSpan.FromMinutes(1),
        };
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);
        var command = CreateCommand(bootstrap, "fault-stop");
        var payload = JsonNode.Parse(command.Payload)!.AsObject();
        payload["faultEvidenceDeadline"] = JsonValue.Create(DateTimeOffset.UtcNow.AddMilliseconds(100));

        var result = await executor.ExecuteAsync(
            command with { Payload = payload.ToJsonString() },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("fault-evidence-deadline-expired", result.ErrorCode);
        Assert.True(storage.PublishCancellationObserved);
    }

    [Fact]
    public async Task Terminate_ForceStopsWithoutStorageTransfer()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => true);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);

        var result = await executor.ExecuteAsync(CreateCommand(bootstrap, "terminate"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, processController.StopCount);
        Assert.True(processController.LastForce);
        Assert.Equal(TimeSpan.Zero, processController.LastTimeout);
        Assert.Equal(1, processController.LastGracefulAttempts);
        Assert.Equal(0, storage.ApplyCount);
        Assert.Equal(0, storage.PublishCount);
    }

    [Fact]
    public async Task SupersessionCompensation_TerminatesOnlyRuntimeCreatingActions()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var processController = new RecordingProcessController();
        var storage = new RecordingStorageTransfer(() => true);
        var executor = CreateExecutor(directory.Path, bootstrap, processController, storage);

        await executor.CompensateSupersededAsync(
            CreateCommand(bootstrap, "collect-logs"),
            CancellationToken.None);
        Assert.Equal(0, processController.StopCount);

        await executor.CompensateSupersededAsync(
            CreateCommand(bootstrap, "start"),
            CancellationToken.None);
        Assert.Equal(1, processController.StopCount);
        Assert.True(processController.LastForce);
    }

    private static Arma3WorkerCommandExecutor CreateExecutor(
        string root,
        CloudWorkerBootstrapConfig bootstrap,
        IArma3ProcessController processController,
        ICloudWorkerStorageTransfer storageTransfer)
    {
        var options = new CloudWorkerOptions(
            Path.Combine(root, "bootstrap.json"),
            Path.Combine(root, "state"),
            Path.Combine(root, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                Path.Combine(root, "steamcmd"),
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = Path.Combine(root, "tmp"),
            });
        return new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            processController: processController,
            storageTransfer: storageTransfer);
    }

    private static WorkerCommandClaimResponse CreateCommand(
        CloudWorkerBootstrapConfig bootstrap,
        string action)
        => FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId,
            action);

    private sealed class RecordingProcessController : IArma3ProcessController
    {
        public int ObserveCount { get; private set; }
        public int StopCount { get; private set; }
        public bool LastForce { get; private set; }
        public TimeSpan LastTimeout { get; private set; }
        public int LastGracefulAttempts { get; private set; }
        public WorkerCommandExecutionResult StopResult { get; init; }
            = WorkerCommandExecutionResult.Success("stopped");

        public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
        {
            ObserveCount++;
            return Task.FromResult(new Arma3ProcessObservation(false, null, null, null));
        }

        public Task<WorkerCommandExecutionResult> StartAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("started"));

        public Task<WorkerCommandExecutionResult> StopAsync(
            bool force,
            TimeSpan timeout,
            int gracefulAttempts,
            CancellationToken cancellationToken)
        {
            StopCount++;
            LastForce = force;
            LastTimeout = timeout;
            LastGracefulAttempts = gracefulAttempts;
            return Task.FromResult(StopResult);
        }
    }

    private sealed class RecordingStorageTransfer(Func<bool> stoppedProbe) : ICloudWorkerStorageTransfer
    {
        public int ApplyCount { get; private set; }
        public int PublishCount { get; private set; }
        public bool PublishedAfterStop { get; private set; }
        public Exception? PublishException { get; init; }
        public TimeSpan PublishDelay { get; init; }
        public bool PublishCancellationObserved { get; private set; }

        public Task ResetPersistentOverlayAsync(
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
        {
            ApplyCount++;
            return Task.CompletedTask;
        }

        public async Task PublishPreservationAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
        {
            PublishCount++;
            PublishedAfterStop = stoppedProbe();
            if (PublishException is not null)
                throw PublishException;
            try
            {
                if (PublishDelay > TimeSpan.Zero)
                    await Task.Delay(PublishDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                PublishCancellationObserved = true;
                throw;
            }
        }
    }
}
