using System.Net;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Runs exactly one deployment-bound worker on the persistent control channel.
/// </summary>
/// <remarks>
/// <para>
/// Registration and credential rotation stay on HTTP because they belong to the credential lifecycle. Everything
/// else travels over <see cref="IWorkerControlChannel"/>: the worker never polls. Three activities run until the
/// worker is cancelled or one of them fails fatally. The heartbeat loop owns its own timer, so a long installation
/// never starves the control plane of liveness. The command loop claims on start, after a command finished and after
/// a wake push, and otherwise waits for a wake. While a command executes, a wake makes the worker send a Running
/// probe, and a superseded answer cancels the execution and compensates exactly as before.
/// </para>
/// <para>
/// The local command journal, redelivery reconciliation, terminal re-reporting and lease-expiry handling are
/// unchanged, so a dropped channel never duplicates execution: an unacknowledged transition is simply delivered again
/// and reconciled against the journal. <see cref="CloudWorkerOptions.PollInterval"/> is only the retry delay after a
/// transient channel failure.
/// </para>
/// <para>
/// Terminal reports are built from the journal entry, and the journal bounds command output to 16 KiB and the error
/// message to 2 KiB before it writes them. That keeps every report far below the channel's 256 KiB frame limit,
/// above which the control plane closes the whole channel with status 1009.
/// </para>
/// </remarks>
/// <param name="apiClient">The HTTP client used for registration and credential rotation only.</param>
/// <param name="channel">The persistent control channel used for heartbeats, claims, reports and metrics.</param>
/// <param name="credentialStore">The owner-only store that persists the worker credential.</param>
/// <param name="commandJournal">The durable local journal of command transitions.</param>
/// <param name="commandExecutor">The game-specific executor of commands.</param>
/// <param name="metricsSampler">The sampler of machine readings, used by the primary host only.</param>
/// <param name="supportedGameIdentifiers">The games this worker image implements, advertised at registration.</param>
public sealed class CloudWorkerRuntime(
    ICloudWorkerApiClient apiClient,
    IWorkerControlChannel channel,
    CloudWorkerCredentialStore credentialStore,
    FlatFileWorkerCommandJournal commandJournal,
    IWorkerCommandExecutor commandExecutor,
    IWorkerMetricsSampler metricsSampler,
    IReadOnlyCollection<string> supportedGameIdentifiers)
{
    /// <summary>The stage reported by heartbeats while no command executes.</summary>
    private const string IdleStage = "idle";

    /// <summary>The service identifier of the primary host, the only host that is metered.</summary>
    private const string PrimaryServiceIdentifier = "game-server";

    private volatile string _stage = IdleStage;

    /// <summary>
    /// Registers when required, starts the channel and runs the heartbeat and command activities.
    /// </summary>
    /// <param name="connection">The non-secret worker identity and control-plane location.</param>
    /// <param name="bootstrap">The bootstrap material, required only for the first registration.</param>
    /// <param name="options">The process-local timing options.</param>
    /// <param name="cancellationToken">A token that stops the worker.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="InvalidOperationException">
    /// Registration material is missing or expired, or the persisted credential has expired.
    /// </exception>
    /// <exception cref="WorkerChannelAuthenticationException">The control plane rejected the credential.</exception>
    public async Task<int> RunAsync(
        CloudWorkerConnectionConfig connection,
        CloudWorkerBootstrapConfig? bootstrap,
        CloudWorkerOptions options,
        CancellationToken cancellationToken)
    {
        connection.Validate();
        ValidateTransport(connection, options.AllowInsecureHttp);
        var storedCredential = await credentialStore.ReadAsync(cancellationToken).ConfigureAwait(false);
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
                        ProtocolVersion = WorkerChannelProtocol.ProtocolVersion,
                        SupportedGameIdentifiers = [.. supportedGameIdentifiers],
                        SupportedContentProviderIdentifiers = ["steam-workshop", "uploaded-artifact", "git"],
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
        }
        else
        {
            EnsureSameConnection(connection, storedCredential.Connection);
        }

        var credentialHolder = new CredentialHolder(storedCredential);
        // A due rotation happens before the first connect so the channel never presents a credential that is
        // about to be replaced.
        await EnsureCredentialCurrentAsync(connection, credentialHolder, false, cancellationToken)
            .ConfigureAwait(false);
        await channel.StartAsync(
                connection,
                _ => Task.FromResult(credentialHolder.Current.Credential),
                cancellationToken)
            .ConfigureAwait(false);

        using var activities = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var firstHeartbeat = SendFirstHeartbeatAsync(connection, options, activities.Token);
        var firstFinished = await Task.WhenAny(firstHeartbeat, channel.Completion).ConfigureAwait(false);
        if (firstFinished != firstHeartbeat)
        {
            activities.Cancel();
            await ObserveAsync([firstHeartbeat]).ConfigureAwait(false);
            await firstFinished.ConfigureAwait(false);
            return 0;
        }
        await firstHeartbeat.ConfigureAwait(false);

        var heartbeatLoop = RunHeartbeatLoopAsync(connection, credentialHolder, options, activities.Token);
        var commandLoop = RunCommandLoopAsync(connection, options, activities.Token);
        var running = new List<Task> { heartbeatLoop, commandLoop };
        // Only the primary host (the game-server service, ordinal 0) is metered; other hosts send no samples.
        if (string.Equals(connection.ServiceIdentifier, PrimaryServiceIdentifier, StringComparison.Ordinal)
            && connection.ServiceOrdinal == 0)
        {
            running.Add(RunMetricsLoopAsync(connection, options, activities.Token));
        }
        var finished = await Task.WhenAny([.. running, channel.Completion]).ConfigureAwait(false);
        activities.Cancel();
        await ObserveAsync(running.Where(q => q != finished)).ConfigureAwait(false);
        if (finished == commandLoop)
            return await commandLoop.ConfigureAwait(false);
        await finished.ConfigureAwait(false);
        return 0;
    }

    private static async Task ObserveAsync(IEnumerable<Task> tasks)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // The activity that finished first owns the reported outcome; later faults caused by the
                // cancellation that follows it are not interesting.
            }
        }
    }

    private async Task SendFirstHeartbeatAsync(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await SendHeartbeatAsync(connection, _stage, cancellationToken).ConfigureAwait(false);
                // The first successful authenticated request proves that a persisted pending
                // registration credential reached this worker, so bootstrap recovery is no longer needed.
                TryRetireBootstrap(options.BootstrapPath);
                return;
            }
            catch (WorkerChannelUnavailableException)
            {
                ReportTransientChannelFailure();
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sends heartbeats on their own timer so command execution never delays liveness, and keeps the credential
    /// current.
    /// </summary>
    private async Task RunHeartbeatLoopAsync(
        CloudWorkerConnectionConfig connection,
        CredentialHolder credentialHolder,
        CloudWorkerOptions options,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, credentialHolder.Current.HeartbeatIntervalSeconds));
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            await EnsureCredentialCurrentAsync(connection, credentialHolder, true, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await SendHeartbeatAsync(connection, _stage, cancellationToken).ConfigureAwait(false);
                TryRetireBootstrap(options.BootstrapPath);
            }
            catch (WorkerChannelUnavailableException)
            {
                // Execution is locally durable; a missed heartbeat is retried at the next interval.
                ReportTransientChannelFailure();
            }
        }
    }

    /// <summary>
    /// Sends one machine and player sample per metrics interval until cancelled.
    /// </summary>
    /// <remarks>
    /// Samples are lossy by design: a sample the channel cannot deliver is dropped, never queued, retried or
    /// replayed, so a gap in history stays a gap and no value is ever invented. Every figure is a measured value or
    /// a reason from the sampler and the executor; an unmeasured figure is never sent as zero.
    /// </remarks>
    private async Task RunMetricsLoopAsync(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        CancellationToken cancellationToken)
    {
        // The first CPU reading has no previous counters and reports measuring; take it now so the first sample
        // that is sent already carries a measured CPU value.
        metricsSampler.Sample();
        using var timer = new PeriodicTimer(options.MetricsInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var sample = await BuildMetricsSampleAsync(connection, cancellationToken).ConfigureAwait(false);
            try
            {
                await channel.SendMetricsAsync(sample, cancellationToken).ConfigureAwait(false);
            }
            catch (WorkerChannelUnavailableException)
            {
                // Dropped on purpose; the next interval sends a fresh sample.
            }
        }
    }

    private async Task<WorkerMetricsSample> BuildMetricsSampleAsync(
        CloudWorkerConnectionConfig connection,
        CancellationToken cancellationToken)
    {
        var machine = metricsSampler.Sample();
        WorkerPlayerObservation players;
        try
        {
            players = await commandExecutor.ObservePlayersAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            players = WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.QueryFailed);
        }
        return new WorkerMetricsSample
        {
            DeploymentId = connection.DeploymentId,
            SampledAt = DateTimeOffset.UtcNow,
            CpuPercent = machine.CpuPercent,
            CpuUnavailableReason = machine.CpuUnavailableReason,
            MemoryUsedBytes = machine.MemoryUsedBytes,
            MemoryTotalBytes = machine.MemoryTotalBytes,
            MemoryUnavailableReason = machine.MemoryUnavailableReason,
            PlayerCount = players.Count,
            // A capacity travels only together with a count, and only when it is a positive slot count that holds
            // the count; anything else is reported as unknown rather than as a value the API would reject.
            PlayerCapacity = players.Count is { } count && players.Capacity is > 0 and var capacity && capacity >= count
                ? capacity
                : null,
            PlayersUnavailableReason = players.UnavailableReason,
        };
    }

    /// <summary>
    /// Claims on start, after a finished command and after each wake push; otherwise waits for a wake.
    /// </summary>
    private async Task<int> RunCommandLoopAsync(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var command = await channel.ClaimNextAsync(cancellationToken).ConfigureAwait(false);
                if (command is null)
                {
                    if (options.ClaimOnce)
                        return 0;
                    await channel.WaitForWakeAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await ProcessCommandAsync(connection, command, cancellationToken).ConfigureAwait(false);
                if (options.ClaimOnce)
                    return 0;
            }
            catch (WorkerChannelUnavailableException)
            {
                ReportTransientChannelFailure();
                await channel.WaitUntilConnectedAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        return 0;
    }

    /// <summary>
    /// Fails when the credential expired and rotates it over HTTP when it is due. A replacement is saved durably
    /// before it is used, then the channel reconnects so the next connect presents it.
    /// </summary>
    private async Task EnsureCredentialCurrentAsync(
        CloudWorkerConnectionConfig connection,
        CredentialHolder credentialHolder,
        bool reconnect,
        CancellationToken cancellationToken)
    {
        if (credentialHolder.Current.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException(
                "The persisted worker credential has expired; a replacement bootstrap is required.");
        }
        StoredWorkerCredential rotated;
        try
        {
            rotated = await RotateCredentialIfRequiredAsync(connection, credentialHolder.Current, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
        {
            ReportTransientControlPlaneFailure(ex);
            return;
        }
        if (ReferenceEquals(rotated, credentialHolder.Current))
            return;
        credentialHolder.Current = rotated;
        if (reconnect)
            await channel.ReconnectAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Journals, executes and reports one claimed command over the channel.
    /// </summary>
    /// <param name="connection">The non-secret worker identity.</param>
    /// <param name="command">The claimed command.</param>
    /// <param name="cancellationToken">A token that abandons the command without recording an outcome.</param>
    /// <returns>A task that completes when the command reached a reported or journaled outcome.</returns>
    /// <exception cref="WorkerChannelUnavailableException">
    /// The channel dropped before a transition was acknowledged; the control plane redelivers the command and the
    /// journal reconciles it.
    /// </exception>
    public async Task ProcessCommandAsync(
        CloudWorkerConnectionConfig connection,
        WorkerCommandClaimResponse command,
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
                        command,
                        terminal,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            existing = await commandJournal.RecordReceivedAsync(command, cancellationToken).ConfigureAwait(false);
            var accepted = await ReportAsync(
                    connection,
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
        _stage = running.Stage;
        var executionTask = commandExecutor.ExecuteAsync(command, commandCancellation.Token);
        try
        {
            var superseded = await WaitForExecutionOrSupersessionAsync(
                    connection,
                    command,
                    running.Stage,
                    executionTask,
                    cancellationToken)
                .ConfigureAwait(false);
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
        finally
        {
            _stage = IdleStage;
        }
    }

    /// <summary>
    /// Waits for the execution to finish. Every wake push that arrives meanwhile triggers a Running probe.
    /// </summary>
    /// <returns><see langword="true"/> when the control plane superseded the command.</returns>
    private async Task<bool> WaitForExecutionOrSupersessionAsync(
        CloudWorkerConnectionConfig connection,
        WorkerCommandClaimResponse command,
        string stage,
        Task<WorkerCommandExecutionResult> executionTask,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var wakeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var wakeTask = channel.WaitForWakeAsync(wakeCancellation.Token);
            var finished = await Task.WhenAny(executionTask, wakeTask).ConfigureAwait(false);
            if (finished == executionTask)
            {
                // Cancelling the wait leaves a push that has not arrived yet unconsumed. The next claim after the
                // command finishes covers one that was consumed in the same instant.
                wakeCancellation.Cancel();
                try
                {
                    await wakeTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                return false;
            }
            await wakeTask.ConfigureAwait(false);
            try
            {
                if (!await ReportAsync(
                        connection,
                        command.CommandId,
                        EWorkerCommandReportState.Running,
                        stage,
                        null,
                        cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch (WorkerChannelUnavailableException)
            {
                // A transient probe failure does not prove supersession. The next wake probes again.
                ReportTransientChannelFailure();
            }
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
        WorkerCommandClaimResponse command,
        WorkerCommandJournalEntry entry,
        CancellationToken cancellationToken)
    {
        if (string.Equals(entry.Outcome?.ErrorCode, "command-superseded", StringComparison.Ordinal))
        {
            await commandExecutor.CompensateSupersededAsync(command, cancellationToken).ConfigureAwait(false);
            await ReportJournalEntryAsync(connection, entry, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (await ReportJournalEntryAsync(connection, entry, cancellationToken)
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
        string stage,
        CancellationToken cancellationToken)
    {
        var runtime = await commandExecutor.ObserveRuntimeAsync(cancellationToken).ConfigureAwait(false);
        await channel.HeartbeatAsync(
                new WorkerHeartbeatRequest
                {
                    DeploymentId = connection.DeploymentId,
                    Stage = stage,
                    ProcessId = runtime?.ProcessId,
                    ProcessStartedAt = runtime?.ProcessStartedAt,
                    DeploymentStorageTotalBytes = runtime?.DeploymentStorageTotalBytes,
                    DeploymentStorageAvailableBytes = runtime?.DeploymentStorageAvailableBytes,
                    ControlledDeploymentBytes = runtime?.ControlledDeploymentBytes,
                    Workloads = runtime?.Workloads ?? [],
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<bool> ReportJournalEntryAsync(
        CloudWorkerConnectionConfig connection,
        WorkerCommandJournalEntry entry,
        CancellationToken cancellationToken)
        => ReportAsync(
            connection,
            entry.CommandId,
            entry.State is EWorkerCommandJournalState.Succeeded
                ? EWorkerCommandReportState.Succeeded
                : EWorkerCommandReportState.Failed,
            entry.Stage,
            entry.Outcome,
            cancellationToken);

    private Task<bool> ReportAsync(
        CloudWorkerConnectionConfig connection,
        Guid commandId,
        EWorkerCommandReportState state,
        string stage,
        WorkerCommandExecutionResult? outcome,
        CancellationToken cancellationToken)
        => channel.ReportCommandAsync(
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
                outcome?.ProcessStartedAt,
                outcome?.StorageMeasurements,
                outcome?.ObservedContentVersions)
            {
                Workloads = outcome?.Workloads ?? [],
            },
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

    private static void ReportTransientChannelFailure()
        => Console.Error.WriteLine("Cloud worker control channel is temporarily unavailable.");

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

    /// <summary>
    /// Holds the credential currently in use so the channel's credential provider always reads the latest one.
    /// </summary>
    /// <param name="initial">The credential in use at start.</param>
    private sealed class CredentialHolder(StoredWorkerCredential initial)
    {
        private StoredWorkerCredential _current = initial;

        /// <summary>Gets or sets the credential currently in use.</summary>
        /// <value>The latest durably saved credential.</value>
        public StoredWorkerCredential Current
        {
            get => Volatile.Read(ref _current);
            set => Volatile.Write(ref _current, value);
        }
    }
}
