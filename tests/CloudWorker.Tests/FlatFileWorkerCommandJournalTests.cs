using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class FlatFileWorkerCommandJournalTests
{
    [Fact]
    public async Task TerminalOutcome_SurvivesRestartAndPreventsRegression()
    {
        using var directory = new TemporaryDirectory();
        var command = CreateCommand();
        var first = new FlatFileWorkerCommandJournal(directory.Path);
        await first.RecordReceivedAsync(command);
        await first.RecordRunningAsync(command, "starting");
        await first.RecordSucceededAsync(
            command,
            WorkerCommandExecutionResult.Success("started", processId: 123, processStartedAt: DateTimeOffset.UtcNow));

        var restarted = new FlatFileWorkerCommandJournal(directory.Path);
        var entry = await restarted.RecordReceivedAsync(command);

        Assert.Equal(EWorkerCommandJournalState.Succeeded, entry.State);
        Assert.Equal(123, entry.Outcome?.ProcessId);
        Assert.DoesNotContain("payload", await File.ReadAllTextAsync(
            System.IO.Path.Combine(directory.Path, "command-journal", $"{command.CommandId:N}.json")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Redelivery_WithChangedAction_IsRejected()
    {
        using var directory = new TemporaryDirectory();
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var command = CreateCommand();
        await journal.RecordReceivedAsync(command);

        var changed = command with { ActionIdentifier = "terminate" };

        await Assert.ThrowsAsync<InvalidDataException>(() => journal.RecordReceivedAsync(changed));
    }

    [Fact]
    public async Task Redelivery_WithChangedCommandContent_IsRejectedWithoutPersistingPayload()
    {
        using var directory = new TemporaryDirectory();
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var command = CreateCommand();
        await journal.RecordReceivedAsync(command);

        var changed = command with { Payload = command.Payload.Replace("\"arma3\"", "\"other\"") };

        await Assert.ThrowsAsync<InvalidDataException>(() => journal.RecordReceivedAsync(changed));
        var persisted = await File.ReadAllTextAsync(
            System.IO.Path.Combine(directory.Path, "command-journal", $"{command.CommandId:N}.json"));
        Assert.DoesNotContain(command.Payload, persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunningRedelivery_PreservesOriginalProcessReconciliationContext()
    {
        using var directory = new TemporaryDirectory();
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var command = CreateCommand(action: "restart");
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await journal.RecordReceivedAsync(command);
        await journal.RecordRunningAsync(
            command,
            "restarting",
            new WorkerRuntimeStatus("running", 123, startedAt, null));

        var redelivered = await journal.RecordRunningAsync(
            command,
            "restarting-after-recovery",
            new WorkerRuntimeStatus("stopped", null, null, null));

        Assert.Equal(123, redelivered.ProcessIdBeforeExecution);
        Assert.Equal(startedAt, redelivered.ProcessStartedAtBeforeExecution);
    }

    [Fact]
    public async Task FailureOutcome_RedactsSensitiveLines()
    {
        using var directory = new TemporaryDirectory();
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var command = CreateCommand();

        var entry = await journal.RecordFailedAsync(
            command,
            WorkerCommandExecutionResult.Failure("failed", "external", "password=do-not-store"));

        Assert.Equal("[redacted sensitive worker output]", entry.Outcome?.ErrorMessage);
    }

    [Fact]
    public async Task FailureOutcome_PreservesInnocuousSecurityVocabulary()
    {
        using var directory = new TemporaryDirectory();
        var journal = new FlatFileWorkerCommandJournal(directory.Path);
        var command = CreateCommand();
        const string diagnostic = "password policy healthy; token bucket full; secretariat online";

        var entry = await journal.RecordFailedAsync(
            command,
            WorkerCommandExecutionResult.Failure("failed", "external", diagnostic));

        Assert.Equal(diagnostic, entry.Outcome?.ErrorMessage);
    }

    [Fact]
    public void SecretRedactor_ReplacesKnownValueWithoutHidingInnocuousLine()
    {
        const string diagnostic = "connection rejected value hunter2; password policy healthy";

        var sanitized = WorkerSecretRedactor.Redact(diagnostic, ["hunter2"]);

        Assert.Equal("connection rejected value ****************; password policy healthy", sanitized);
    }

    internal static WorkerCommandClaimResponse CreateCommand(
        Guid? workerId = null,
        Guid? deploymentId = null,
        Guid? serverId = null,
        string action = "start")
    {
        var actualDeploymentId = deploymentId ?? Guid.NewGuid();
        var payload = JsonSerializer.Serialize(
            new WorkerCommandPayload
            {
                ServerId = serverId ?? Guid.NewGuid(),
                DeploymentId = actualDeploymentId,
                GameIdentifier = "arma3",
                ActionIdentifier = action,
                GracefulStopAttempts = 2,
                GracefulStopTimeoutSeconds = 30,
                FaultEvidenceDeadline = string.Equals(action, "fault-stop", StringComparison.OrdinalIgnoreCase)
                    ? DateTimeOffset.UtcNow.AddMinutes(5)
                    : null,
            },
            CloudWorkerJson.Options);
        return new WorkerCommandClaimResponse
        {
            CommandId = Guid.NewGuid(),
            LifecycleJobId = Guid.NewGuid(),
            DeploymentId = actualDeploymentId,
            WorkerId = workerId ?? Guid.NewGuid(),
            Sequence = 1,
            ActionIdentifier = action,
            Payload = payload,
            Attempt = 1,
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
        };
    }
}
