using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class FlatFileWorkerCommandJournal(string stateDirectory)
{
    private readonly string _directory = Path.Combine(Path.GetFullPath(stateDirectory), "command-journal");
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<WorkerCommandJournalEntry?> ReadAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("Command identity cannot be empty.", nameof(commandId));
        var path = EntryPath(commandId);
        if (!File.Exists(path))
            return null;
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<WorkerCommandJournalEntry>(
                stream,
                CloudWorkerJson.Options,
                cancellationToken)
            .ConfigureAwait(false)
               ?? throw new InvalidDataException("Worker command journal entry is empty.");
    }

    public Task<WorkerCommandJournalEntry> RecordReceivedAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken = default)
        => TransitionAsync(command, EWorkerCommandJournalState.Received, "received", null, null, cancellationToken);

    public Task<WorkerCommandJournalEntry> RecordRunningAsync(
        WorkerCommandClaimResponse command,
        string stage,
        WorkerRuntimeStatus? runtimeBeforeExecution = null,
        CancellationToken cancellationToken = default)
        => TransitionAsync(
            command,
            EWorkerCommandJournalState.Running,
            stage,
            runtimeBeforeExecution,
            null,
            cancellationToken);

    public Task<WorkerCommandJournalEntry> RecordSucceededAsync(
        WorkerCommandClaimResponse command,
        WorkerCommandExecutionResult outcome,
        CancellationToken cancellationToken = default)
        => TransitionAsync(
            command,
            EWorkerCommandJournalState.Succeeded,
            outcome.Stage,
            null,
            outcome,
            cancellationToken);

    public Task<WorkerCommandJournalEntry> RecordFailedAsync(
        WorkerCommandClaimResponse command,
        WorkerCommandExecutionResult outcome,
        CancellationToken cancellationToken = default)
        => TransitionAsync(
            command,
            EWorkerCommandJournalState.Failed,
            outcome.Stage,
            null,
            outcome,
            cancellationToken);

    public Task<WorkerCommandJournalEntry> RecordSupersededAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken = default)
        => TransitionAsync(
            command,
            EWorkerCommandJournalState.Failed,
            "cancelled-by-cost-stop",
            null,
            WorkerCommandExecutionResult.Failure(
                "cancelled-by-cost-stop",
                "command-superseded",
                "The control plane superseded this command with a durable cost-stop barrier."),
            cancellationToken,
            allowTerminalSupersession: true);

    private async Task<WorkerCommandJournalEntry> TransitionAsync(
        WorkerCommandClaimResponse command,
        EWorkerCommandJournalState requestedState,
        string stage,
        WorkerRuntimeStatus? runtimeBeforeExecution,
        WorkerCommandExecutionResult? outcome,
        CancellationToken cancellationToken,
        bool allowTerminalSupersession = false)
    {
        ValidateCommand(command);
        ValidateTransition(requestedState, stage, runtimeBeforeExecution, outcome);
        var gate = _locks.GetOrAdd(command.CommandId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await ReadAsync(command.CommandId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                VerifyAttribution(existing, command);
                if (existing.State is EWorkerCommandJournalState.Succeeded or EWorkerCommandJournalState.Failed)
                {
                    if (!allowTerminalSupersession
                        || string.Equals(
                            existing.Outcome?.ErrorCode,
                            "command-superseded",
                            StringComparison.Ordinal))
                    {
                        return existing;
                    }
                }
                if (existing.State is EWorkerCommandJournalState.Running
                    && requestedState is EWorkerCommandJournalState.Received)
                {
                    return existing;
                }
            }

            var now = DateTimeOffset.UtcNow;
            var effectiveState = existing?.State is EWorkerCommandJournalState.Running
                                 && requestedState is EWorkerCommandJournalState.Received
                ? EWorkerCommandJournalState.Running
                : requestedState;
            var entry = new WorkerCommandJournalEntry(
                command.CommandId,
                command.DeploymentId,
                command.WorkerId,
                command.Sequence,
                HashAction(command.ActionIdentifier),
                HashContent(command.Payload),
                effectiveState,
                Bound(stage, 128),
                existing?.TimeStampReceived ?? now,
                now,
                requestedState is EWorkerCommandJournalState.Running
                && existing?.State is not EWorkerCommandJournalState.Running
                    ? runtimeBeforeExecution?.ProcessId
                    : existing?.ProcessIdBeforeExecution,
                requestedState is EWorkerCommandJournalState.Running
                && existing?.State is not EWorkerCommandJournalState.Running
                    ? runtimeBeforeExecution?.ProcessStartedAt
                    : existing?.ProcessStartedAtBeforeExecution,
                outcome is null ? null : SanitizeOutcome(outcome));
            await WriteAsync(entry, cancellationToken).ConfigureAwait(false);
            return entry;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task WriteAsync(WorkerCommandJournalEntry entry, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(entry, CloudWorkerJson.Options);
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(_directory, entry.CommandId.ToString("N"), ".tmp", json, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, EntryPath(entry.CommandId), true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(EntryPath(entry.CommandId), UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private string EntryPath(Guid commandId) => Path.Combine(_directory, $"{commandId:N}.json");

    private static void VerifyAttribution(WorkerCommandJournalEntry entry, WorkerCommandClaimResponse command)
    {
        if (entry.CommandId != command.CommandId
            || entry.DeploymentId != command.DeploymentId
            || entry.WorkerId != command.WorkerId
            || entry.Sequence != command.Sequence
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(entry.ActionIdentifierHash),
                Convert.FromHexString(HashAction(command.ActionIdentifier)))
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(entry.CommandContentHash),
                Convert.FromHexString(HashContent(command.Payload))))
        {
            throw new InvalidDataException("A redelivered command does not match its durable worker attribution.");
        }
    }

    private static void ValidateCommand(WorkerCommandClaimResponse command)
    {
        if (command.CommandId == Guid.Empty || command.DeploymentId == Guid.Empty || command.WorkerId == Guid.Empty)
            throw new InvalidDataException("Command journal attribution is incomplete.");
        if (command.Sequence <= 0
            || string.IsNullOrWhiteSpace(command.ActionIdentifier)
            || command.Payload is null)
        {
            throw new InvalidDataException("Command journal metadata is invalid.");
        }
    }

    private static string HashAction(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())));

    private static string HashContent(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void ValidateTransition(
        EWorkerCommandJournalState requestedState,
        string stage,
        WorkerRuntimeStatus? runtimeBeforeExecution,
        WorkerCommandExecutionResult? outcome)
    {
        if (string.IsNullOrWhiteSpace(stage))
            throw new InvalidDataException("Worker command journal stage is required.");
        if (runtimeBeforeExecution is not null
            && ((runtimeBeforeExecution.ProcessId is null) != (runtimeBeforeExecution.ProcessStartedAt is null)
                || runtimeBeforeExecution.ProcessId is <= 0))
        {
            throw new InvalidDataException("Worker command reconciliation process evidence is invalid.");
        }
        if (requestedState is EWorkerCommandJournalState.Succeeded or EWorkerCommandJournalState.Failed)
        {
            if (outcome is null || outcome.Succeeded != (requestedState is EWorkerCommandJournalState.Succeeded))
                throw new InvalidDataException("Worker command terminal outcome does not match its journal state.");
            if ((outcome.ProcessId is null) != (outcome.ProcessStartedAt is null) || outcome.ProcessId is <= 0)
                throw new InvalidDataException("Worker command outcome process evidence is invalid.");
            if (!outcome.Succeeded
                && (string.IsNullOrWhiteSpace(outcome.ErrorCode) || string.IsNullOrWhiteSpace(outcome.ErrorMessage)))
            {
                throw new InvalidDataException("Worker command failure outcome is incomplete.");
            }
        }
    }

    private static WorkerCommandExecutionResult SanitizeOutcome(WorkerCommandExecutionResult outcome)
        => outcome with
        {
            Stage = Bound(outcome.Stage, 128),
            ErrorCode = BoundNullable(outcome.ErrorCode, 128),
            ErrorMessage = WorkerSecretRedactor.Redact(BoundNullable(outcome.ErrorMessage, 2048)),
            Output = WorkerSecretRedactor.Redact(BoundNullable(outcome.Output, 16 * 1024)),
        };

    private static string Bound(string value, int length)
        => value.Length <= length ? value : value[..length];

    private static string? BoundNullable(string? value, int length)
        => value is null ? null : Bound(value, length);
}