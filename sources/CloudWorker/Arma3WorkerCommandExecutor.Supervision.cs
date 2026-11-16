using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Coordinates colocated Arma processes while retaining a single worker and command authority.</summary>
public sealed partial class Arma3WorkerCommandExecutor
{
    /// <summary>Serializes process mutations against idle dependency-loss cleanup.</summary>
    private readonly SemaphoreSlim _supervisionGate = new(1, 1);

    /// <summary>Controls the dependent process independently of the primary process.</summary>
    private readonly IArma3ProcessController _auxiliaryProcessController;

    /// <summary>Separates dependent mutable state from the shared installation.</summary>
    private readonly Arma3RuntimePaths _auxiliaryPaths;

    /// <summary>Gets the worker-owned operation state outside preserved game files.</summary>
    /// <value>The atomic supervision journal path.</value>
    private string SupervisionStatePath => Path.Combine(_paths.AgentStatePath, "arma3-supervision.json");

    /// <summary>Validates assigned workloads and selects the two-process execution path.</summary>
    /// <param name="payload">The attributed command.</param>
    /// <returns>Whether this host must supervise both supported Arma workloads.</returns>
    /// <exception cref="InvalidDataException">The assignment is ambiguous or unsupported.</exception>
    private bool HasColocatedWorkloads(Arma3CommandPayload payload)
    {
        if (File.Exists(SupervisionStatePath)
            && payload.Command.ActionIdentifier is "stop" or "terminate" or "fault-stop")
            return true;
        var assignments = payload.Command.Workloads;
        if (assignments.Count == 0)
            return false;
        if (assignments.Count > 32
            || assignments.GroupBy(q => (q.ServiceIdentifier, q.ServiceOrdinal)).Any(q => q.Count() != 1))
            throw new InvalidDataException("The workload assignment contains duplicate or excessive identities.");
        var local = assignments.Where(q => q.HostServiceIdentifier == _connection.ServiceIdentifier
                                          && q.HostServiceOrdinal == _connection.ServiceOrdinal).ToArray();
        if (local.Length == 1 && local[0].ServiceIdentifier == _connection.ServiceIdentifier
                              && local[0].ServiceOrdinal == _connection.ServiceOrdinal)
            return false;
        if (_connection.ServiceIdentifier != "game-server" || _connection.ServiceOrdinal != 0
            || local.Length != 2
            || !local.Any(q => q.ServiceIdentifier == "game-server" && q.ServiceOrdinal == 0 && q.IsPrimary)
            || !local.Any(q => q.ServiceIdentifier == "headless-client" && q.ServiceOrdinal == 0 && !q.IsPrimary)
            || !payload.Command.IsPrimaryService)
            throw new InvalidDataException("The worker was assigned an unsupported Arma workload group.");
        return true;
    }

    /// <summary>Projects the local dependent workload into Arma's existing endpoint configuration API.</summary>
    /// <param name="payload">The host endpoint and frozen workload assignment.</param>
    /// <returns>A worker-local topology with the colocated endpoint, without changing the durable command.</returns>
    /// <exception cref="InvalidDataException">The primary endpoint is absent or ambiguous.</exception>
    private static Arma3CommandPayload WithColocatedTopology(Arma3CommandPayload payload)
    {
        var primary = payload.Command.Topology.SingleOrDefault(q => q.IsPrimary)
                      ?? throw new InvalidDataException("The colocated group requires its primary endpoint.");
        return payload with
        {
            Command = payload.Command with
            {
                Topology = payload.Command.Topology.Where(q => q.ServiceIdentifier != "headless-client")
                    .Append(primary with { ServiceIdentifier = "headless-client", ServiceOrdinal = 0,
                        IsPrimary = false, Port = null }).ToArray(),
            },
        };
    }

    /// <summary>Resumes one durable operation over the entire local process group.</summary>
    /// <param name="command">The immutable command envelope.</param>
    /// <param name="payload">The validated Arma settings with ephemeral secrets.</param>
    /// <param name="cancellationToken">Cancellation from worker shutdown or supersession.</param>
    /// <returns>The aggregate outcome with observations for both workloads.</returns>
    private async Task<WorkerCommandExecutionResult> ExecuteColocatedAsync(
        WorkerCommandClaimResponse command,
        Arma3CommandPayload payload,
        CancellationToken cancellationToken)
    {
        await _supervisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ExecuteColocatedCoreAsync(command, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _supervisionGate.Release();
        }
    }

    /// <summary>Executes group phases while exclusively owning local process mutations.</summary>
    /// <param name="command">The immutable command envelope.</param>
    /// <param name="payload">Validated settings with ephemeral secrets.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The aggregate process outcome.</returns>
    private async Task<WorkerCommandExecutionResult> ExecuteColocatedCoreAsync(
        WorkerCommandClaimResponse command, Arma3CommandPayload payload, CancellationToken cancellationToken)
    {
        var action = command.ActionIdentifier.ToLowerInvariant();
        if (action is "start" or "restart" or "install" or "update")
            payload = WithColocatedTopology(payload);
        if (action == "collect-logs")
            return await CollectLogsRestartSafeAsync(cancellationToken).ConfigureAwait(false);
        var state = await ReadSupervisionStateAsync(cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.Payload)));
        if (state?.CommandId == command.CommandId && state.PayloadHash != hash)
            throw new InvalidDataException("The supervised command changed during redelivery.");
        if (state?.CommandId != command.CommandId)
        {
            using var primary = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
            using var auxiliary = await _auxiliaryProcessController.ObserveAsync(cancellationToken).ConfigureAwait(false);
            state = new Arma3SupervisionState(_connection.DeploymentId, command.CommandId, hash,
                EArma3SupervisionPhase.Received, primary.IsRunning || auxiliary.IsRunning, []);
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        if (state is null)
            throw new InvalidDataException("The supervision journal is unavailable.");
        if (action is "stop" or "terminate" or "fault-stop")
        {
            try
            {
                return await StopColocatedAsync(command, payload, state, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (action == "fault-stop" && !cancellationToken.IsCancellationRequested)
            {
                return FaultEvidenceDeadlineExpired("The group evidence deadline expired during shutdown.");
            }
        }
        if (action is not ("start" or "restart" or "install" or "update"))
            return WorkerCommandExecutionResult.Failure("action-unsupported", "unsupported-action",
                "The requested worker action is not supported.");
        if (state.Phase == EArma3SupervisionPhase.Failed)
            return await FailColocatedAsync(state,
                WorkerCommandExecutionResult.Failure("group-failed", "group-recovery-required",
                    "A failed process group requires a new authorized command."), cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await StartColocatedAsync(command, payload, state, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                return await FailColocatedAsync(state, result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled launch must not leave the dependent or primary process running independently.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await FailColocatedAsync(state,
                WorkerCommandExecutionResult.Failure("group-cancelled", "command-cancelled", "The operation was cancelled."),
                cleanup.Token).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                      or ArgumentException or ContentAcquisitionException)
        {
            return await FailColocatedAsync(state,
                WorkerCommandExecutionResult.Failure("group-failed", "group-start-failed",
                    WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets()) ?? "Group startup failed."),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Prepares one shared installation and launches each role only at its durable phase.</summary>
    /// <param name="command">The command binding downloaded content.</param>
    /// <param name="payload">The effective primary settings.</param>
    /// <param name="state">The operation's last durable phase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Process-running evidence; it does not assert an engine-confirmed join.</returns>
    private async Task<WorkerCommandExecutionResult> StartColocatedAsync(
        WorkerCommandClaimResponse command, Arma3CommandPayload payload,
        Arma3SupervisionState state, CancellationToken cancellationToken)
    {
        var action = command.ActionIdentifier.ToLowerInvariant();
        var maintenance = action is "install" or "update";
        if (state.Phase is EArma3SupervisionPhase.Received or EArma3SupervisionPhase.Preparing)
        {
            var stopped = await StopColocatedProcessesAsync(payload, false, cancellationToken).ConfigureAwait(false);
            if (!stopped.Succeeded)
                return stopped;
            state = state with { Phase = EArma3SupervisionPhase.Preparing };
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
            // Both roles consume the same files; role filtering happens only in the launch plans.
            var union = payload with
            {
                Command = payload.Command with
                {
                    Content = payload.Command.Content.Select(q => q with { UsageRole = "Shared" }).ToArray(),
                },
            };
            var prepared = await InstallAsync(union, action == "update" || payload.Validate, cancellationToken)
                .ConfigureAwait(false);
            if (!prepared.Succeeded)
                return prepared;
            if (!maintenance)
                await _storageTransfer.ResetPersistentOverlayAsync(_paths.DeploymentRoot, cancellationToken)
                    .ConfigureAwait(false);
            await _renderer.RenderAsync(payload, _paths, cancellationToken).ConfigureAwait(false);
            if (!maintenance)
                await _storageTransfer.ApplyPersistentSnapshotAsync(command, _paths.DeploymentRoot, cancellationToken)
                    .ConfigureAwait(false);
            var observed = await _contentAcquisition.AcquireAndInstallAsync(command, payload.Command.Content,
                "arma3", _paths.InstallPath, cancellationToken, includeClientOnly: true, includeServerOnly: true)
                .ConfigureAwait(false);
            await ReconcileColocatedConfigurationAsync(payload, cancellationToken).ConfigureAwait(false);
            state = state with
            {
                Phase = EArma3SupervisionPhase.Prepared,
                ContentVersions = observed.Concat(ObserveWorkshopVersions(payload.Command.Content, true))
                    .Concat(ObserveWorkshopVersions(payload.Command.Content, false)).Distinct().ToArray(),
            };
            // Publishing this atomic manifest is the only boundary that permits either consumer to start.
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        if (!await Arma3LaunchTopologyContext.MatchesAsync(payload, _paths, cancellationToken).ConfigureAwait(false))
            return TopologyRestartRequired();
        if (maintenance && !state.ResumeRunning)
            return await WithColocatedEvidenceAsync(WorkerCommandExecutionResult.Success("group-prepared") with
                { ObservedContentVersions = state.ContentVersions }, cancellationToken).ConfigureAwait(false);
        if (state.Phase == EArma3SupervisionPhase.Prepared)
        {
            state = state with { Phase = EArma3SupervisionPhase.PrimaryStarting };
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        if (state.Phase == EArma3SupervisionPhase.PrimaryStarting)
        {
            var primary = await _processController.StartAsync(BuildLaunchArguments(payload), cancellationToken)
                .ConfigureAwait(false);
            if (!primary.Succeeded)
                return primary;
            using var observedPrimary = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
            state = state with { Phase = EArma3SupervisionPhase.PrimaryRunning,
                Primary = ToWorkloadObservation("game-server", observedPrimary) };
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        using (var primary = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!primary.IsRunning || primary.Error is not null
                || state.Primary?.ProcessId != primary.Identity?.ProcessId
                || state.Primary?.ProcessStartedAt != primary.Identity?.ProcessStartedAt)
                return WorkerCommandExecutionResult.Failure("primary-missing", "primary-process-missing",
                    "The primary process is not available for its dependent workload.");
        }
        if (state.Phase == EArma3SupervisionPhase.PrimaryRunning)
        {
            state = state with { Phase = EArma3SupervisionPhase.AuxiliaryStarting };
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        if (state.Phase == EArma3SupervisionPhase.AuxiliaryStarting)
        {
            _auxiliaryPaths.EnsureDirectories();
            var auxiliaryPayload = payload with
            {
                Command = payload.Command with
                {
                    ServiceIdentifier = "headless-client", ServiceOrdinal = 0, IsPrimaryService = false,
                    PreserveOnStop = false,
                    Topology = payload.Command.Topology.Select(q => q.IsPrimary ? q with { Address = "127.0.0.1" } : q)
                        .ToArray(),
                },
            };
            var arguments = BuildAuxiliaryLaunchArguments(auxiliaryPayload)
                .Select(q => q.StartsWith("-profiles=", StringComparison.Ordinal)
                    ? $"-profiles={_auxiliaryPaths.ProfilePath}" : q).ToArray();
            var auxiliary = await _auxiliaryProcessController.StartAsync(arguments, cancellationToken).ConfigureAwait(false);
            if (!auxiliary.Succeeded)
                return auxiliary;
            using var observedAuxiliary = await _auxiliaryProcessController.ObserveAsync(cancellationToken).ConfigureAwait(false);
            state = state with { Phase = EArma3SupervisionPhase.Running,
                Auxiliary = ToWorkloadObservation("headless-client", observedAuxiliary) };
            await WriteSupervisionStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        var result = await WithColocatedEvidenceAsync(WorkerCommandExecutionResult.Success("processes-running") with
        {
            ObservedContentVersions = state.ContentVersions,
            StorageMeasurements = MeasurePreparedContent(payload.GetSteamWorkshopItemIds()
                .Concat((payload with { Command = payload.Command with { IsPrimaryService = false } })
                    .GetSteamWorkshopItemIds()).Distinct().ToArray()),
        }, cancellationToken).ConfigureAwait(false);
        return result.Workloads.All(q => q.State == EWorkerWorkloadState.Running)
               && result.Workloads.Any(q => q.ServiceIdentifier == "headless-client"
                   && q.ProcessId == state.Auxiliary?.ProcessId
                   && q.ProcessStartedAt == state.Auxiliary?.ProcessStartedAt)
            ? result
            : result with { Succeeded = false, ErrorCode = "group-process-missing",
                ErrorMessage = "A required process exited before group startup completed." };
    }

    /// <summary>Applies deployment-owned admission after every restored or installed overlay.</summary>
    /// <param name="payload">The original host-scoped settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The asynchronous reconciliation.</returns>
    private Task ReconcileColocatedConfigurationAsync(Arma3CommandPayload payload, CancellationToken cancellationToken)
        => PrepareAdmissionAsync(payload, cancellationToken);

    /// <summary>Stops every writer before publishing the primary's persistent data once.</summary>
    /// <param name="command">The command authorized to publish preservation.</param>
    /// <param name="payload">Stop policy and deadline.</param>
    /// <param name="state">Current operation phase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shutdown and preservation outcome.</returns>
    private async Task<WorkerCommandExecutionResult> StopColocatedAsync(
        WorkerCommandClaimResponse command, Arma3CommandPayload payload,
        Arma3SupervisionState state, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (payload.Command.FaultEvidenceDeadline is { } expires)
        {
            if (expires <= DateTimeOffset.UtcNow)
                return FaultEvidenceDeadlineExpired("The group evidence deadline expired.");
            deadline.CancelAfter(expires - DateTimeOffset.UtcNow);
        }
        var token = deadline.Token;
        string? diagnostics = null;
        if (command.ActionIdentifier == "fault-stop")
        {
            try
            {
                diagnostics = (await CollectLogsRestartSafeAsync(token).ConfigureAwait(false)).Output;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                diagnostics = "Runtime diagnostics could not be collected.";
            }
        }
        if (state.Phase == EArma3SupervisionPhase.Stopped)
        {
            var repeated = await WithColocatedEvidenceAsync(WorkerCommandExecutionResult.Success("group-stopped"), token)
                .ConfigureAwait(false);
            if (repeated.Workloads.All(q => q.State == EWorkerWorkloadState.Stopped))
                return repeated;
        }
        state = state with { Phase = EArma3SupervisionPhase.Stopping };
        await WriteSupervisionStateAsync(state, token).ConfigureAwait(false);
        var stopped = await StopColocatedProcessesAsync(payload, command.ActionIdentifier == "terminate", token)
            .ConfigureAwait(false);
        if (!stopped.Succeeded)
            return await WithColocatedEvidenceAsync(stopped, token).ConfigureAwait(false);
        if (command.ActionIdentifier != "terminate" && payload.Command.PreserveOnStop)
            await _storageTransfer.PublishPreservationAsync(command, _paths.DeploymentRoot, token).ConfigureAwait(false);
        await WriteSupervisionStateAsync(state with { Phase = EArma3SupervisionPhase.Stopped }, token).ConfigureAwait(false);
        return await WithColocatedEvidenceAsync(stopped with
        {
            Output = diagnostics is null ? BuildPreservationEvidence(stopped)
                : BuildFaultEvidenceOutput(diagnostics, BuildPreservationEvidence(stopped)),
        }, token)
            .ConfigureAwait(false);
    }

    /// <summary>Attempts both shutdowns even when the dependent process cannot be stopped.</summary>
    /// <param name="payload">Optional graceful-stop policy.</param>
    /// <param name="force">Whether to terminate immediately.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A failure unless both process exits were verified.</returns>
    private async Task<WorkerCommandExecutionResult> StopColocatedProcessesAsync(
        Arma3CommandPayload? payload, bool force, CancellationToken cancellationToken)
    {
        var auxiliary = await StopOneProcessAsync(_auxiliaryProcessController, payload, force, cancellationToken)
            .ConfigureAwait(false);
        var primary = await StopOneProcessAsync(_processController, payload, force, cancellationToken)
            .ConfigureAwait(false);
        return !auxiliary.Succeeded ? auxiliary : !primary.Succeeded ? primary
            : WorkerCommandExecutionResult.Success(auxiliary.Stage == "force-stopped" || primary.Stage == "force-stopped"
                ? "force-stopped" : "group-stopped");
    }

    /// <summary>Converts one shutdown failure into evidence so the other workload is still attempted.</summary>
    /// <param name="controller">The independently attributed process controller.</param>
    /// <param name="payload">The graceful shutdown policy.</param>
    /// <param name="force">Whether immediate termination was authorized.</param>
    /// <param name="cancellationToken">The operation deadline or cancellation.</param>
    /// <returns>A verified exit or a bounded, redacted failure.</returns>
    private async Task<WorkerCommandExecutionResult> StopOneProcessAsync(
        IArma3ProcessController controller, Arma3CommandPayload? payload, bool force,
        CancellationToken cancellationToken)
    {
        try
        {
            return force
                ? await controller.TerminateAsync(cancellationToken).ConfigureAwait(false)
                : await controller.StopAsync(payload!.Force, StopTimeout(payload),
                    payload.Command.GracefulStopAttempts, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                      or OperationCanceledException)
        {
            return WorkerCommandExecutionResult.Failure("group-stop-unverified", "process-stop-unverified",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets()) ?? "Process shutdown failed.");
        }
    }

    /// <summary>Records a failed operation and compensates both local runtime side effects.</summary>
    /// <param name="state">The failed operation identity.</param>
    /// <param name="failure">The original failure to retain.</param>
    /// <param name="cancellationToken">Bounded cleanup cancellation.</param>
    /// <returns>The failure enriched with current process observations.</returns>
    private async Task<WorkerCommandExecutionResult> FailColocatedAsync(
        Arma3SupervisionState state, WorkerCommandExecutionResult failure, CancellationToken cancellationToken)
    {
        await WriteSupervisionStateAsync(state with { Phase = EArma3SupervisionPhase.Failed }, cancellationToken)
            .ConfigureAwait(false);
        var cleanup = await StopColocatedProcessesAsync(null, true, cancellationToken).ConfigureAwait(false);
        return await WithColocatedEvidenceAsync(failure with
        {
            Output = cleanup.Succeeded ? "Both local processes were stopped." : "Local process cleanup remains unverified.",
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Observes both workloads without equating liveness with engine-level admission.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The group process status and deployment storage measurement.</returns>
    private async Task<WorkerRuntimeStatus> ObserveColocatedRuntimeAsync(CancellationToken cancellationToken)
    {
        // Heartbeats remain observable during long preparation, but only idle observers may mutate processes.
        if (await _supervisionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var state = await ReadSupervisionStateAsync(cancellationToken).ConfigureAwait(false);
                using var dependency = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
                if (state?.Phase == EArma3SupervisionPhase.Running && !dependency.IsRunning)
                    await _auxiliaryProcessController.TerminateAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _supervisionGate.Release();
            }
        }
        using var primary = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        using var auxiliary = await _auxiliaryProcessController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        var storage = ObserveDeploymentStorage();
        return new WorkerRuntimeStatus(primary.IsRunning && auxiliary.IsRunning ? "processes-running" : "group-incomplete",
            primary.IsRunning && primary.Error is null ? primary.Identity?.ProcessId : null,
            primary.IsRunning && primary.Error is null ? primary.Identity?.ProcessStartedAt : null,
            "Process observations do not verify an Arma Headless Client join.",
            storage.TotalBytes, storage.AvailableBytes, storage.ControlledBytes)
        {
            Workloads = [ToWorkloadObservation("game-server", primary), ToWorkloadObservation("headless-client", auxiliary)],
        };
    }

    /// <summary>Projects exact process evidence into the generic worker protocol.</summary>
    /// <param name="service">The worker-owned workload identifier.</param>
    /// <param name="observation">Verified local process evidence.</param>
    /// <returns>A current process observation without a readiness assertion.</returns>
    private static WorkerWorkloadObservation ToWorkloadObservation(string service, Arma3ProcessObservation observation)
        => new()
        {
            ServiceIdentifier = service, ServiceOrdinal = 0,
            ProcessId = observation.IsRunning && observation.Error is null ? observation.Identity?.ProcessId : null,
            ProcessStartedAt = observation.IsRunning && observation.Error is null ? observation.Identity?.ProcessStartedAt : null,
            State = observation.Error is not null ? EWorkerWorkloadState.Unknown
                : observation.IsRunning ? EWorkerWorkloadState.Running : EWorkerWorkloadState.Stopped,
            ObservedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Adds both process observations to one durable command outcome.</summary>
    /// <param name="result">The operation result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result with current workload evidence.</returns>
    private async Task<WorkerCommandExecutionResult> WithColocatedEvidenceAsync(
        WorkerCommandExecutionResult result, CancellationToken cancellationToken)
    {
        var runtime = await ObserveColocatedRuntimeAsync(cancellationToken).ConfigureAwait(false);
        return result with { ProcessId = runtime.ProcessId, ProcessStartedAt = runtime.ProcessStartedAt,
            Workloads = runtime.Workloads };
    }

    /// <summary>Reads and validates the local deployment's last operation.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation state, or null before the first colocated command.</returns>
    /// <exception cref="InvalidDataException">The stored state is not attributable to this deployment.</exception>
    private async Task<Arma3SupervisionState?> ReadSupervisionStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SupervisionStatePath))
            return null;
        await using var stream = new FileStream(SupervisionStatePath, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete);
        var state = await JsonSerializer.DeserializeAsync<Arma3SupervisionState>(stream, CloudWorkerJson.Options,
            cancellationToken).ConfigureAwait(false);
        if (state is null || state.DeploymentId != _connection.DeploymentId || !Enum.IsDefined(state.Phase))
            throw new InvalidDataException("The group journal does not match this deployment.");
        return state;
    }

    /// <summary>Atomically persists a secret-free phase on the agent-state filesystem.</summary>
    /// <param name="state">The next operation state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The asynchronous durable write.</returns>
    private async Task WriteSupervisionStateAsync(Arma3SupervisionState state, CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        using var temporary = await RestrictedTemporaryFile.CreateAsync(_paths.AgentStatePath,
            "arma3-supervision", ".tmp", JsonSerializer.Serialize(state, CloudWorkerJson.Options), cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, SupervisionStatePath, true);
    }
}
