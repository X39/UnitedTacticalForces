using System.Net;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Maintains the outbound heartbeat/claim channel for exactly one deployment-bound worker.
/// </summary>
public sealed class CloudWorkerRuntime(
    ICloudWorkerApiClient apiClient,
    CloudWorkerCredentialStore credentialStore,
    FlatFileWorkerCommandJournal commandJournal,
    IWorkerCommandExecutor commandExecutor)
{
    public async Task<int> RunAsync(
        CloudWorkerConnectionConfig connection,
        CloudWorkerBootstrapConfig? bootstrap,
        CloudWorkerOptions options,
        CancellationToken cancellationToken)
    {
        connection.Validate();
        ValidateTransport(connection, options.AllowInsecureHttp);
        var storedCredential = await credentialStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        var heartbeatInterval = options.HeartbeatInterval;
        if (storedCredential is null)
        {
            if (bootstrap is null)
                throw new InvalidOperationException("Worker registration requires bootstrap material.");
            EnsureSameConnection(connection, bootstrap);
            if (bootstrap.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Worker bootstrap has expired before registration.");
            var registration = await apiClient.RegisterAsync(
                    bootstrap,
                    new WorkerRegistrationRequest
                    {
                        DeploymentId = bootstrap.DeploymentId,
                        Identity = $"cloud-{bootstrap.WorkerId:N}",
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            storedCredential = new StoredWorkerCredential(
                connection,
                registration.WorkerCredential,
                registration.Response.CredentialExpiresAt,
                registration.Response.HeartbeatIntervalSeconds,
                DateTimeOffset.UtcNow);
            await credentialStore.SaveAsync(storedCredential, cancellationToken).ConfigureAwait(false);
            heartbeatInterval = TimeSpan.FromSeconds(storedCredential.HeartbeatIntervalSeconds);
        }
        else
        {
            EnsureSameConnection(connection, storedCredential.Connection);
            heartbeatInterval = TimeSpan.FromSeconds(storedCredential.HeartbeatIntervalSeconds);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (storedCredential.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException(
                    "The persisted worker credential has expired; a replacement bootstrap is required.");
            }
            try
            {
                storedCredential = await RotateCredentialIfRequiredAsync(
                        connection,
                        storedCredential,
                        cancellationToken)
                    .ConfigureAwait(false);
                heartbeatInterval = TimeSpan.FromSeconds(storedCredential.HeartbeatIntervalSeconds);
                await SendHeartbeatAsync(
                        connection,
                        storedCredential.Credential,
                        "idle",
                        cancellationToken)
                    .ConfigureAwait(false);
                // The first successful authenticated request proves that a persisted pending
                // registration credential reached this worker, so bootstrap recovery is no longer needed.
                TryRetireBootstrap(options.BootstrapPath);
                var command = await apiClient
                    .ClaimNextAsync(connection, storedCredential.Credential, cancellationToken)
                    .ConfigureAwait(false);
                if (command is null)
                {
                    if (options.ClaimOnce)
                        return 0;
                    await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await ProcessCommandAsync(
                        connection,
                        storedCredential.Credential,
                        command,
                        heartbeatInterval,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (options.ClaimOnce)
                    return 0;
            }
            catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
            {
                ReportTransientControlPlaneFailure(ex);
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        return 0;
    }

    private async Task<StoredWorkerCredential> RotateCredentialIfRequiredAsync(
        CloudWorkerConnectionConfig connection,
        StoredWorkerCredential current,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!ShouldRotateCredential(current, now))
            return current;

        var rotation = await apiClient
            .RotateCredentialAsync(connection, current.Credential, cancellationToken)
            .ConfigureAwait(false);
        var replacement = new StoredWorkerCredential(
            connection,
            rotation.WorkerCredential,
            rotation.Response.CredentialExpiresAt,
            current.HeartbeatIntervalSeconds,
            now);

        // Save uses an owner-only temporary file and atomic replacement. The pending credential
        // is never used until this durable write succeeds; the API therefore keeps current valid
        // if the response or local save is lost.
        await credentialStore.SaveAsync(replacement, cancellationToken).ConfigureAwait(false);
        return replacement;
    }

    private static bool ShouldRotateCredential(StoredWorkerCredential credential, DateTimeOffset now)
    {
        var lifetime = credential.IssuedAt is { } issuedAt && credential.ExpiresAt > issuedAt
            ? credential.ExpiresAt - issuedAt
            : TimeSpan.FromDays(30);
        var proportionalLead = TimeSpan.FromTicks(Math.Max(1, lifetime.Ticks / 10));
        var heartbeatLead = TimeSpan.FromSeconds(Math.Max(1, credential.HeartbeatIntervalSeconds) * 4D);
        var lead = proportionalLead > heartbeatLead ? proportionalLead : heartbeatLead;
        if (lead > TimeSpan.FromDays(1))
            lead = TimeSpan.FromDays(1);
        var halfLifetime = TimeSpan.FromTicks(Math.Max(1, lifetime.Ticks / 2));
        if (lead > halfLifetime)
            lead = halfLifetime;
        return credential.ExpiresAt - now <= lead;
    }

    public async Task ProcessCommandAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        WorkerCommandClaimResponse command,
        TimeSpan heartbeatInterval,
        CancellationToken cancellationToken)
    {
        command.Validate(connection);
        var existing = await commandJournal.ReadAsync(command.CommandId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            // RecordReceived performs the fixed-time attribution/hash check even when
            // the existing entry is already terminal.
            existing = await commandJournal.RecordReceivedAsync(command, cancellationToken).ConfigureAwait(false);
            if (existing.State is EWorkerCommandJournalState.Succeeded or EWorkerCommandJournalState.Failed)
            {
                await ReportTerminalOrSupersedeAsync(
                        connection,
                        workerCredential,
                        command,
                        existing,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            if (existing.State is EWorkerCommandJournalState.Running)
            {
                var reconciled = await commandExecutor
                    .ReconcileRedeliveryAsync(command, existing, cancellationToken)
                    .ConfigureAwait(false);
                if (reconciled is not null)
                {
                    var terminal = reconciled.Succeeded
                        ? await commandJournal.RecordSucceededAsync(command, reconciled, cancellationToken)
                            .ConfigureAwait(false)
                        : await commandJournal.RecordFailedAsync(command, reconciled, cancellationToken)
                            .ConfigureAwait(false);
                    await ReportTerminalOrSupersedeAsync(
                            connection,
                            workerCredential,
                            command,
                            terminal,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }
            }
        }
        else
        {
            if (command.LeaseExpiresAt <= DateTimeOffset.UtcNow)
            {
                var expired = WorkerCommandExecutionResult.Failure(
                    "lease-expired",
                    "command-lease-expired",
                    "The command lease expired before worker execution began.");
                var terminal = await commandJournal
                    .RecordFailedAsync(command, expired, cancellationToken)
                    .ConfigureAwait(false);
                await ReportTerminalOrSupersedeAsync(
                        connection,
                        workerCredential,
                        command,
                        terminal,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            existing = await commandJournal.RecordReceivedAsync(command, cancellationToken).ConfigureAwait(false);
            var accepted = await ReportAsync(
                    connection,
                    workerCredential,
                    command.CommandId,
                    EWorkerCommandReportState.Received,
                    existing.Stage,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!accepted)
            {
                await RecordSupersededAndCompensateAsync(command, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        var runtimeBeforeExecution = await commandExecutor
            .ObserveRuntimeAsync(cancellationToken)
            .ConfigureAwait(false);
        var running = await commandJournal
            .RecordRunningAsync(command, "running", runtimeBeforeExecution, cancellationToken)
            .ConfigureAwait(false);
        var executionAccepted = await ReportAsync(
                connection,
                workerCredential,
                command.CommandId,
                EWorkerCommandReportState.Running,
                running.Stage,
                null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionAccepted)
        {
            await RecordSupersededAndCompensateAsync(command, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var executionTask = commandExecutor.ExecuteAsync(command, commandCancellation.Token);
        try
        {
            var interval = heartbeatInterval > TimeSpan.Zero ? heartbeatInterval : TimeSpan.FromSeconds(15);
            var continuationProbeInterval = interval < TimeSpan.FromSeconds(1)
                ? interval
                : TimeSpan.FromSeconds(1);
            var nextHeartbeat = DateTimeOffset.UtcNow + interval;
            var superseded = false;
            while (await Task.WhenAny(
                       executionTask,
                       Task.Delay(continuationProbeInterval, cancellationToken)).ConfigureAwait(false)
                   != executionTask)
            {
                if (DateTimeOffset.UtcNow >= nextHeartbeat)
                {
                    try
                    {
                        await SendHeartbeatAsync(
                                connection,
                                workerCredential,
                                running.Stage,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        // Execution is locally durable and must not be duplicated merely because
                        // a progress heartbeat could not reach the control plane.
                        ReportTransientControlPlaneFailure(ex);
                    }
                    nextHeartbeat = DateTimeOffset.UtcNow + interval;
                }

                try
                {
                    if (await ReportAsync(
                            connection,
                            workerCredential,
                            command.CommandId,
                            EWorkerCommandReportState.Running,
                            running.Stage,
                            null,
                            cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }
                    superseded = true;
                    break;
                }
                catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
                {
                    // A transient probe failure does not prove supersession. Retry on the next interval.
                    ReportTransientControlPlaneFailure(ex);
                }
            }
            if (superseded)
            {
                commandCancellation.Cancel();
                await ObserveCancellationAsync(executionTask, commandCancellation.Token).ConfigureAwait(false);
                await RecordSupersededAndCompensateAsync(command, cancellationToken).ConfigureAwait(false);
                return;
            }
            var outcome = await executionTask.ConfigureAwait(false);
            var completed = outcome.Succeeded
                ? await commandJournal.RecordSucceededAsync(command, outcome, cancellationToken).ConfigureAwait(false)
                : await commandJournal.RecordFailedAsync(command, outcome, cancellationToken).ConfigureAwait(false);
            var terminalAccepted = await ReportJournalEntryAsync(
                    connection,
                    workerCredential,
                    completed,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!terminalAccepted)
            {
                commandCancellation.Cancel();
                await RecordSupersededAndCompensateAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            commandCancellation.Cancel();
            try
            {
                await executionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (commandCancellation.IsCancellationRequested)
            {
            }
            catch
            {
                // Preserve the protocol/storage exception that made the runtime abandon this execution.
            }
            throw;
        }
    }

    private async Task RecordSupersededAndCompensateAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        await commandExecutor.CompensateSupersededAsync(command, cancellationToken).ConfigureAwait(false);
        await commandJournal.RecordSupersededAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportTerminalOrSupersedeAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        WorkerCommandClaimResponse command,
        WorkerCommandJournalEntry entry,
        CancellationToken cancellationToken)
    {
        if (string.Equals(entry.Outcome?.ErrorCode, "command-superseded", StringComparison.Ordinal))
        {
            await commandExecutor.CompensateSupersededAsync(command, cancellationToken).ConfigureAwait(false);
            await ReportJournalEntryAsync(connection, workerCredential, entry, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (await ReportJournalEntryAsync(connection, workerCredential, entry, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }
        await RecordSupersededAndCompensateAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ObserveCancellationAsync(
        Task<WorkerCommandExecutionResult> executionTask,
        CancellationToken commandCancellation)
    {
        try
        {
            await executionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (commandCancellation.IsCancellationRequested)
        {
        }
        catch
        {
            // Cost-stop supersession owns the durable outcome even if local cancellation races a worker failure.
        }
    }

    private async Task SendHeartbeatAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        string stage,
        CancellationToken cancellationToken)
    {
        var runtime = await commandExecutor.ObserveRuntimeAsync(cancellationToken).ConfigureAwait(false);
        await apiClient.HeartbeatAsync(
                connection,
                workerCredential,
                new WorkerHeartbeatRequest
                {
                    DeploymentId = connection.DeploymentId,
                    Stage = stage,
                    ProcessId = runtime?.ProcessId,
                    ProcessStartedAt = runtime?.ProcessStartedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<bool> ReportJournalEntryAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        WorkerCommandJournalEntry entry,
        CancellationToken cancellationToken)
        => ReportAsync(
            connection,
            workerCredential,
            entry.CommandId,
            entry.State is EWorkerCommandJournalState.Succeeded
                ? EWorkerCommandReportState.Succeeded
                : EWorkerCommandReportState.Failed,
            entry.Stage,
            entry.Outcome,
            cancellationToken);

    private Task<bool> ReportAsync(
        CloudWorkerConnectionConfig connection,
        string workerCredential,
        Guid commandId,
        EWorkerCommandReportState state,
        string stage,
        WorkerCommandExecutionResult? outcome,
        CancellationToken cancellationToken)
        => apiClient.ReportCommandAsync(
            connection,
            workerCredential,
            commandId,
            new WorkerCommandReportRequest(
                connection.DeploymentId,
                state,
                stage,
                DateTimeOffset.UtcNow,
                outcome?.ErrorCode,
                outcome?.ErrorMessage,
                outcome?.Output,
                outcome?.ProcessId,
                outcome?.ProcessStartedAt),
            cancellationToken);

    private static void ValidateTransport(CloudWorkerConnectionConfig connection, bool allowInsecureHttp)
    {
        var uri = new Uri(connection.ApiBaseUrl);
        if (uri.Scheme == Uri.UriSchemeHttps || allowInsecureHttp || uri.IsLoopback)
            return;
        throw new InvalidOperationException("Cloud worker API transport must use HTTPS.");
    }

    private static void EnsureSameConnection(
        CloudWorkerConnectionConfig expected,
        CloudWorkerConnectionConfig actual)
    {
        if (expected.WorkerId != actual.WorkerId
            || expected.DeploymentId != actual.DeploymentId
            || expected.ServerId != actual.ServerId
            || !string.Equals(expected.ApiBaseUrl, actual.ApiBaseUrl, StringComparison.Ordinal)
            || !string.Equals(expected.GameIdentifier, actual.GameIdentifier, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(expected.WorkerApiPath, actual.WorkerApiPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Persisted worker connection identity does not match this runtime.");
        }
    }

    private static void ReportTransientControlPlaneFailure(HttpRequestException exception)
        => Console.Error.WriteLine(
            $"Cloud worker control-plane request is temporarily unavailable "
            + $"({exception.StatusCode?.ToString() ?? "network"}).");

    private static void TryRetireBootstrap(string path)
    {
        try
        {
            File.Delete(Path.GetFullPath(path));
        }
        catch (IOException)
        {
            // Read-only Docker/Kubernetes secret mounts are expected and expire externally.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
