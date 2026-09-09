using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Minimal native Arma 3 worker implementation. Provider concerns never enter this type.
/// </summary>
public sealed partial class Arma3WorkerCommandExecutor : IWorkerCommandExecutor
{
    private const string SteamTemporaryDirectoryPrefix = "utf-steamcmd";
    private const string SteamRunScriptExtension = ".runscript";
    private const int MaximumCommandDiagnosticCharacters = 16 * 1024;
    private const int MaximumErrorMessageCharacters = 2048;
    private const int MaximumSteamLogTailBytes = 4 * 1024;
    private const int MaximumDiagnosticConfigurationBytes = 1024 * 1024;
    private static readonly string[] SteamDiagnosticLogNames =
    [
        "bootstrap_log.txt",
        "connection_log.txt",
        "console_log.txt",
        "content_log.txt",
        "stderr.txt",
        "workshop_log.txt",
    ];
    private readonly CloudWorkerConnectionConfig _connection;
    private readonly CloudWorkerOptions _options;
    private readonly Arma3RuntimePaths _paths;
    private readonly Arma3ConfigurationRenderer _renderer;
    private readonly IProcessCommandRunner _commandRunner;
    private readonly IArma3ProcessController _processController;
    private readonly ICloudWorkerStorageTransfer _storageTransfer;
    private readonly RestrictedGameProcessLauncher _processLauncher;
    private readonly object _diagnosticSecretsLock = new();
    private readonly HashSet<string> _diagnosticSecrets = new(StringComparer.Ordinal);
    private readonly string _steamTemporaryRoot;
    private readonly string _steamHomeDirectory;
    private readonly WorkerContentAcquisitionService _contentAcquisition;

    public Arma3WorkerCommandExecutor(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        Arma3ConfigurationRenderer? renderer = null,
        IProcessCommandRunner? commandRunner = null,
        IArma3ProcessController? processController = null,
        ICloudWorkerStorageTransfer? storageTransfer = null)
    {
        _connection = connection;
        _options = options;
        _paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.Arma3.ExecutableName);
        _renderer = renderer ?? new Arma3ConfigurationRenderer();
        _commandRunner = commandRunner ?? new ProcessCommandRunner();
        _processLauncher = new RestrictedGameProcessLauncher(options.Arma3);
        var identityStore = new Arma3ProcessIdentityStore(connection.ServerId, connection.DeploymentId, _paths);
        _processController = processController
                             ?? new Arma3ProcessController(
                                 connection.ServerId,
                                  connection.DeploymentId,
                                  _paths,
                                  identityStore,
                                  _processLauncher);
        _storageTransfer = storageTransfer ?? new UnavailableCloudWorkerStorageTransfer();
        _contentAcquisition = new WorkerContentAcquisitionService(options, _storageTransfer, _commandRunner);
        _steamTemporaryRoot = PrepareSteamTemporaryRoot(options.Arma3.TemporaryDirectory);
        _steamHomeDirectory = ResolveSteamHomeDirectory(options);
        CleanupStaleSteamTemporaryArtifacts();
    }

    public async Task<WorkerCommandExecutionResult> ExecuteAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        try
        {
            command.Validate(_connection);
            if (!string.Equals(_connection.GameIdentifier, "arma3", StringComparison.OrdinalIgnoreCase))
            {
                return WorkerCommandExecutionResult.Failure(
                    "game-unsupported",
                    "unsupported-game",
                    "This worker image does not support the assigned game implementation.");
            }
            var payload = Arma3CommandPayload.Parse(command.Payload);
            payload = AddSecretConfiguration(payload, command.Secrets);
            payload.ValidateAttribution(_connection, command);
            RememberPayloadSecrets(payload);
            return command.ActionIdentifier.Trim().ToLowerInvariant() switch
            {
                "install" => await MaintainAsync(payload, payload.Validate, cancellationToken).ConfigureAwait(false),
                "update" => await MaintainAsync(payload, true, cancellationToken).ConfigureAwait(false),
                "start" => await StartWithPreparationAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "stop" => await StopAndPreserveAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "fault-stop" => await FaultStopAndPreserveAsync(command, payload, cancellationToken)
                    .ConfigureAwait(false),
                "terminate" => await _processController.TerminateAsync(cancellationToken).ConfigureAwait(false),
                "restart" => await RestartAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "collect-logs" => await CollectLogsRestartSafeAsync(cancellationToken).ConfigureAwait(false),
                _ => WorkerCommandExecutionResult.Failure(
                    "action-unsupported",
                    "unsupported-action",
                    "The requested worker action is not supported."),
            };
        }
        catch (ContentAcquisitionException ex)
        {
            return WorkerCommandExecutionResult.Failure(
                "content-acquisition-failed",
                ex.Retryable ? $"retryable-{ex.ErrorCode}" : ex.ErrorCode,
                ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                      or InvalidOperationException or ArgumentException)
        {
            return WorkerCommandExecutionResult.Failure(
                "worker-execution-failed",
                "worker-operation-failed",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets()) ?? "Worker operation failed.");
        }
    }

    public async Task<WorkerCommandExecutionResult?> ReconcileRedeliveryAsync(
        WorkerCommandClaimResponse command,
        WorkerCommandJournalEntry journalEntry,
        CancellationToken cancellationToken)
    {
        command.Validate(_connection);
        var payload = Arma3CommandPayload.Parse(command.Payload);
        payload.ValidateAttribution(_connection, command);
        var action = command.ActionIdentifier.Trim().ToLowerInvariant();
        if (action is "fault-stop"
            && payload.Command.FaultEvidenceDeadline <= DateTimeOffset.UtcNow)
        {
            return WorkerCommandExecutionResult.Failure(
                "fault-evidence-window-expired",
                "fault-evidence-deadline-expired",
                "The persisted fault-evidence deadline expired before redelivery reconciliation.");
        }
        using var observation = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
        {
            return WorkerCommandExecutionResult.Failure(
                "redelivery-reconcile-failed",
                "process-identity-invalid",
                observation.Error);
        }
        if (action is "start" && observation.IsRunning && observation.Identity is not null)
        {
            return WorkerCommandExecutionResult.Success(
                "reconciled-running",
                processId: observation.Identity.ProcessId,
                processStartedAt: observation.Identity.ProcessStartedAt);
        }
        if (action is "restart" && observation.IsRunning && observation.Identity is not null)
        {
            return journalEntry.ProcessIdBeforeExecution is null
                   || IsOriginalProcess(journalEntry, observation.Identity)
                ? null
                : WorkerCommandExecutionResult.Success(
                    "reconciled-restarted",
                    processId: observation.Identity.ProcessId,
                    processStartedAt: observation.Identity.ProcessStartedAt);
        }
        if (action is "terminate" && !observation.IsRunning)
            return WorkerCommandExecutionResult.Success("reconciled-stopped");
        if (action is "install" or "update")
        {
            if (observation.IsRunning && observation.Identity is not null)
            {
                if (journalEntry.ProcessIdBeforeExecution is null)
                {
                    return WorkerCommandExecutionResult.Failure(
                        "maintenance-outcome-uncertain",
                        "maintenance-outcome-uncertain",
                        "Maintenance recovery found a process that was not running when execution began.");
                }
                return IsOriginalProcess(journalEntry, observation.Identity)
                    ? null
                    : WorkerCommandExecutionResult.Success(
                        action is "update"
                            ? "reconciled-updated-and-restarted"
                            : "reconciled-installed-and-restarted",
                        processId: observation.Identity.ProcessId,
                        processStartedAt: observation.Identity.ProcessStartedAt);
            }
            if (journalEntry.ProcessIdBeforeExecution is not null)
            {
                return WorkerCommandExecutionResult.Failure(
                    "maintenance-outcome-uncertain",
                    "maintenance-outcome-uncertain",
                    "Maintenance stopped a previously running process before recovery; operator retry is required.");
            }
        }

        // The observed state proves that repeating these handlers is safe. Start and
        // stop reconcile again; SteamCMD installation is idempotent; log reads have no side effect.
        return null;
    }

    public async Task CompensateSupersededAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        command.Validate(_connection);
        var action = command.ActionIdentifier.Trim().ToLowerInvariant();
        if (action is not ("start" or "restart" or "install" or "update"))
            return;
        var terminated = await _processController.TerminateAsync(cancellationToken).ConfigureAwait(false);
        if (!terminated.Succeeded)
        {
            throw new InvalidOperationException(
                terminated.ErrorMessage ?? "Superseded command runtime compensation failed.");
        }
    }

    public async Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken)
    {
        var storage = ObserveDeploymentStorage();
        using var observation = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
        {
            return new WorkerRuntimeStatus(
                "identity-invalid",
                null,
                null,
                observation.Error,
                storage.TotalBytes,
                storage.AvailableBytes,
                storage.ControlledBytes);
        }
        return observation.IsRunning && observation.Identity is not null
            ? new WorkerRuntimeStatus(
                "running",
                observation.Identity.ProcessId,
                observation.Identity.ProcessStartedAt,
                null,
                storage.TotalBytes,
                storage.AvailableBytes,
                storage.ControlledBytes)
            : new WorkerRuntimeStatus(
                IsPrepared() ? "prepared" : "not-installed",
                null,
                null,
                null,
                storage.TotalBytes,
                storage.AvailableBytes,
                storage.ControlledBytes);
    }

    private async Task<WorkerCommandExecutionResult> StartAsync(
        Arma3CommandPayload payload,
        CancellationToken cancellationToken)
    {
        await _renderer.RenderAsync(payload, _paths, cancellationToken).ConfigureAwait(false);
        return await _processController
            .StartAsync(BuildLaunchArguments(payload), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<WorkerCommandExecutionResult> StartWithPreparationAsync(
        WorkerCommandClaimResponse command,
        Arma3CommandPayload payload,
        CancellationToken cancellationToken)
    {
        using var observation = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
        {
            return WorkerCommandExecutionResult.Failure(
                "start-reconcile-failed",
                "process-identity-invalid",
                observation.Error);
        }
        if (observation.IsRunning && observation.Identity is not null)
        {
            return WorkerCommandExecutionResult.Success(
                "already-running",
                processId: observation.Identity.ProcessId,
                processStartedAt: observation.Identity.ProcessStartedAt);
        }
        var workshopItemIds = payload.GetSteamWorkshopItemIds();
        if (!IsPrepared() || !SelectedWorkshopContentExists(workshopItemIds))
        {
            var prepared = await InstallAsync(payload, payload.Validate, cancellationToken).ConfigureAwait(false);
            if (!prepared.Succeeded)
                return prepared with { Stage = "start-prepare-failed" };
        }
        try
        {
            if (payload.Command.IsPrimaryService)
            {
                await _storageTransfer
                    .ResetPersistentOverlayAsync(_paths.DeploymentRoot, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsStorageTransferFailure(ex))
        {
            return WorkerCommandExecutionResult.Failure(
                "start-persistent-overlay-failed",
                "persistent-overlay-failed",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets())
                ?? "Persistent data could not be prepared.");
        }
        await _renderer.RenderAsync(payload, _paths, cancellationToken).ConfigureAwait(false);
        try
        {
            if (payload.Command.IsPrimaryService)
            {
                await _storageTransfer
                    .ApplyPersistentSnapshotAsync(command, _paths.DeploymentRoot, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsStorageTransferFailure(ex))
        {
            return WorkerCommandExecutionResult.Failure(
                "start-persistent-overlay-failed",
                "persistent-overlay-failed",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets())
                ?? "Persistent data could not be prepared.");
        }
        var observed = (await _contentAcquisition
                .AcquireAndInstallAsync(
                    command,
                    payload.Command.Content,
                    "arma3",
                    _paths.InstallPath,
                    cancellationToken,
                    includeClientOnly: !payload.Command.IsPrimaryService,
                    includeServerOnly: payload.Command.IsPrimaryService)
                .ConfigureAwait(false))
            .Concat(ObserveWorkshopVersions(payload.Command.Content, payload.Command.IsPrimaryService))
            .ToArray();
        var started = await _processController
            .StartAsync(BuildLaunchArguments(payload), cancellationToken)
            .ConfigureAwait(false);
        return started with { ObservedContentVersions = observed };
    }

    private async Task<WorkerCommandExecutionResult> StopAndPreserveAsync(
        WorkerCommandClaimResponse command,
        Arma3CommandPayload payload,
        CancellationToken cancellationToken)
    {
        var stopped = await _processController
            .StopAsync(
                false,
                StopTimeout(payload),
                payload.Command.GracefulStopAttempts,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopped.Succeeded)
            return stopped;
        if (!payload.Command.PreserveOnStop)
            return stopped;
        try
        {
            await _storageTransfer
                .PublishPreservationAsync(command, _paths.DeploymentRoot, cancellationToken)
                .ConfigureAwait(false);
            return stopped with { Output = BuildPreservationEvidence(stopped) };
        }
        catch (Exception ex) when (IsStorageTransferFailure(ex))
        {
            return WorkerCommandExecutionResult.Failure(
                "preservation-failed",
                "preservation-transfer-failed",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets())
                ?? "Persistent data could not be published.") with
            {
                Output = BuildPreservationEvidence(stopped),
            };
        }
    }

    private async Task<WorkerCommandExecutionResult> FaultStopAndPreserveAsync(
        WorkerCommandClaimResponse command,
        Arma3CommandPayload payload,
        CancellationToken cancellationToken)
    {
        var deadline = payload.Command.FaultEvidenceDeadline!.Value;
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return WorkerCommandExecutionResult.Failure(
                "fault-evidence-window-expired",
                "fault-evidence-deadline-expired",
                "The bounded fault-evidence deadline expired before worker execution began.");
        }

        using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellation.CancelAfter(remaining);
        var boundedCancellationToken = deadlineCancellation.Token;
        var diagnostics = "Fault diagnostics unavailable.";
        try
        {
            var collected = await CollectLogsRestartSafeAsync(boundedCancellationToken).ConfigureAwait(false);
            diagnostics = collected.Output ?? collected.Stage;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            diagnostics = "Fault diagnostics unavailable: "
                          + (WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets())
                             ?? "log collection failed");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                  && deadlineCancellation.IsCancellationRequested)
        {
            return FaultEvidenceDeadlineExpired(diagnostics);
        }

        try
        {
            var outcome = await StopAndPreserveAsync(command, payload, boundedCancellationToken).ConfigureAwait(false);
            return outcome with
            {
                Output = BuildFaultEvidenceOutput(diagnostics, outcome.Output),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                  && deadlineCancellation.IsCancellationRequested)
        {
            return FaultEvidenceDeadlineExpired(diagnostics);
        }
    }

    private WorkerCommandExecutionResult FaultEvidenceDeadlineExpired(string diagnostics)
        => WorkerCommandExecutionResult.Failure(
            "fault-evidence-window-expired",
            "fault-evidence-deadline-expired",
            "The bounded fault-evidence deadline expired; provider teardown may continue.") with
        {
            Output = BuildFaultEvidenceOutput(diagnostics, null),
        };

    private string BuildFaultEvidenceOutput(string diagnostics, string? stopEvidence)
    {
        var output = new StringBuilder();
        output.AppendLine("Best-effort fault diagnostics:");
        output.AppendLine(diagnostics);
        if (!string.IsNullOrWhiteSpace(stopEvidence))
        {
            output.AppendLine("Stop and preservation evidence:");
            output.AppendLine(stopEvidence);
        }
        return WorkerSecretRedactor.Redact(output.ToString(), SnapshotDiagnosticSecrets()) ?? string.Empty;
    }

    private static string BuildPreservationEvidence(WorkerCommandExecutionResult stopped)
    {
        var evidence = $"ProcessStopOutcome={stopped.Stage}; PreservationConsistency="
                       + (string.Equals(stopped.Stage, "force-stopped", StringComparison.OrdinalIgnoreCase)
                           ? "Potentially inconsistent"
                           : "Consistent");
        return string.IsNullOrWhiteSpace(stopped.Output) ? evidence : $"{stopped.Output}\n{evidence}";
    }

    private async Task<WorkerCommandExecutionResult> RestartAsync(
        WorkerCommandClaimResponse command,
        Arma3CommandPayload payload,
        CancellationToken cancellationToken)
    {
        var stopped = await _processController
            .StopAsync(
                payload.Force,
                StopTimeout(payload),
                payload.Command.GracefulStopAttempts,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopped.Succeeded)
            return stopped with { Stage = "restart-stop-failed" };
        var workshopItemIds = payload.GetSteamWorkshopItemIds();
        if (!IsPrepared() || !SelectedWorkshopContentExists(workshopItemIds))
        {
            var prepared = await InstallAsync(payload, payload.Validate, cancellationToken).ConfigureAwait(false);
            if (!prepared.Succeeded)
                return prepared with { Stage = "restart-prepare-failed" };
        }
        try
        {
            await _storageTransfer
                .ResetPersistentOverlayAsync(_paths.DeploymentRoot, cancellationToken)
                .ConfigureAwait(false);
            await _renderer.RenderAsync(payload, _paths, cancellationToken).ConfigureAwait(false);
            await _storageTransfer
                .ApplyPersistentSnapshotAsync(command, _paths.DeploymentRoot, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStorageTransferFailure(ex))
        {
            return WorkerCommandExecutionResult.Failure(
                "restart-persistent-overlay-failed",
                "persistent-overlay-failed",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets())
                ?? "Persistent data could not be prepared.");
        }
        var observed = (await _contentAcquisition
                .AcquireAndInstallAsync(
                    command,
                    payload.Command.Content,
                    "arma3",
                    _paths.InstallPath,
                    cancellationToken,
                    includeClientOnly: !payload.Command.IsPrimaryService,
                    includeServerOnly: payload.Command.IsPrimaryService)
                .ConfigureAwait(false))
            .Concat(ObserveWorkshopVersions(payload.Command.Content, payload.Command.IsPrimaryService))
            .ToArray();
        var started = await _processController
            .StartAsync(BuildLaunchArguments(payload), cancellationToken)
            .ConfigureAwait(false);
        return started with
        {
            Stage = started.Succeeded ? "restarted" : "restart-start-failed",
            ObservedContentVersions = observed,
        };
    }

    private async Task<WorkerCommandExecutionResult> MaintainAsync(
        Arma3CommandPayload payload,
        bool validate,
        CancellationToken cancellationToken)
    {
        using var observation = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
        {
            return WorkerCommandExecutionResult.Failure(
                "maintenance-reconcile-failed",
                "process-identity-invalid",
                observation.Error);
        }
        var restart = observation.IsRunning;
        if (restart)
        {
            var stopped = await _processController
                .StopAsync(false, StopTimeout(payload), payload.Command.GracefulStopAttempts, cancellationToken)
                .ConfigureAwait(false);
            if (!stopped.Succeeded)
                return stopped with { Stage = "maintenance-stop-failed" };
        }
        var maintained = await InstallAsync(payload, validate, cancellationToken).ConfigureAwait(false);
        if (!maintained.Succeeded || !restart)
            return maintained;
        var started = await StartAsync(payload, cancellationToken).ConfigureAwait(false);
        return started with
        {
            Stage = !started.Succeeded
                ? "maintenance-restart-failed"
                : validate ? "updated-and-restarted" : "installed-and-restarted",
        };
    }

    private async Task<WorkerCommandExecutionResult> InstallAsync(
        Arma3CommandPayload payload,
        bool validate,
        CancellationToken cancellationToken)
    {
        var storageFailure = ValidateStorageCapacity(payload);
        if (storageFailure is not null)
            return storageFailure;
        if (!File.Exists(_options.Arma3.SteamCmdPath))
        {
            return WorkerCommandExecutionResult.Failure(
                "steamcmd-unavailable",
                "steamcmd-not-found",
                "SteamCMD is not installed in the worker image.");
        }
        _paths.EnsureDirectories();
        if (!Directory.Exists(_steamHomeDirectory) && _options.Arma3.SteamUserId is null)
            Directory.CreateDirectory(_steamHomeDirectory);
        if (!Directory.Exists(_steamHomeDirectory))
        {
            return WorkerCommandExecutionResult.Failure(
                "steam-home-unavailable",
                "steam-home-missing",
                "SteamCMD's writable home directory is unavailable.");
        }
        WorkerStoragePath.RejectExistingLinks(_options.RuntimeRoot, _steamHomeDirectory);
        if (File.Exists(_paths.PreparationMarkerPath))
            File.Delete(_paths.PreparationMarkerPath);
        var password = await _options.Arma3.ReadSteamPasswordAsync(cancellationToken).ConfigureAwait(false);
        RememberDiagnosticSecret(password);
        if (!string.Equals(_options.Arma3.SteamUsername, "anonymous", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(password))
        {
            return WorkerCommandExecutionResult.Failure(
                "steam-credentials-unavailable",
                "steam-credentials-missing",
                "Steam credentials are not available through the worker secret mount.");
        }

        var workshopItemIds = payload.GetSteamWorkshopItemIds();
        var script = BuildSteamCmdScript(password, validate, workshopItemIds);
        ProcessCommandResult result;
        var temporaryDirectory = CreateSteamTemporaryDirectory();
        try
        {
            using var scriptFile = await RestrictedTemporaryFile
                .CreateAsync(
                    temporaryDirectory,
                    $"{SteamTemporaryDirectoryPrefix}-{_connection.DeploymentId:N}",
                     SteamRunScriptExtension,
                     script,
                     cancellationToken)
                 .ConfigureAwait(false);
            if (_options.Arma3.SteamUserId is { } steamUserId)
                scriptFile.RestrictToUnixOwner(steamUserId);
            var steamCmdPath = Path.GetFullPath(_options.Arma3.SteamCmdPath);
            var startInfo = _processLauncher.CreateSteamCmd(
                steamCmdPath,
                Path.GetDirectoryName(steamCmdPath)!,
                ["+runscript", scriptFile.Path]);
            startInfo.Environment["HOME"] = _steamHomeDirectory;
            result = await _commandRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteSteamTemporaryDirectory(temporaryDirectory);
        }
        var secrets = SnapshotDiagnosticSecrets();
        var output = WorkerSecretRedactor.Redact(result.Output, secrets) ?? string.Empty;
        var error = WorkerSecretRedactor.Redact(result.Error, secrets) ?? string.Empty;
        if (result.Succeeded && File.Exists(_paths.ExecutablePath))
        {
            if (!SelectedWorkshopContentExists(workshopItemIds))
            {
                return WorkerCommandExecutionResult.Failure(
                    "workshop-prepare-failed",
                    "workshop-content-missing",
                    "SteamCMD completed without all selected Arma Workshop content.");
            }
            await WritePreparationMarkerAsync(cancellationToken).ConfigureAwait(false);
            var measurements = MeasurePreparedContent(workshopItemIds);
            return WorkerCommandExecutionResult.Success(validate ? "updated" : "installed", output) with
            {
                StorageMeasurements = measurements,
            };
        }
        var diagnosticOutput = await CollectSteamFailureDiagnosticsAsync(
                result.ExitCode,
                output,
                error,
                secrets,
                cancellationToken)
            .ConfigureAwait(false);
        return WorkerCommandExecutionResult.Failure(
                "steamcmd-failed",
                "steamcmd-failed",
                BoundTail(
                    string.IsNullOrWhiteSpace(error)
                        ? $"SteamCMD exited with code {result.ExitCode}."
                        : error,
                    MaximumErrorMessageCharacters))
            with
            {
                Output = diagnosticOutput,
            };
    }

    private async Task<string> CollectSteamFailureDiagnosticsAsync(
        int exitCode,
        string output,
        string error,
        IReadOnlyCollection<string> secrets,
        CancellationToken cancellationToken)
    {
        var diagnostics = new StringBuilder();
        AppendDiagnosticSection(diagnostics, "stdout", output);
        AppendDiagnosticSection(diagnostics, "stderr", error);

        var logDirectory = Path.Combine(_steamHomeDirectory, "Steam", "logs");
        foreach (var logName in SteamDiagnosticLogNames)
        {
            var path = Path.Combine(logDirectory, logName);
            try
            {
                if (!File.Exists(path))
                    continue;
                WorkerStoragePath.RejectLink(path);
                var tail = await ReadTailAsync(path, MaximumSteamLogTailBytes, cancellationToken)
                    .ConfigureAwait(false);
                AppendDiagnosticSection(diagnostics, $"logs/{logName}", tail);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                AppendDiagnosticSection(
                    diagnostics,
                    $"logs/{logName}",
                    $"Diagnostic log unavailable: {ex.GetType().Name}.");
            }
        }

        var prefix = $"SteamCMD exit code: {exitCode.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}";
        var redacted = WorkerSecretRedactor.Redact(diagnostics.ToString(), secrets) ?? string.Empty;
        return prefix + BoundTail(redacted, MaximumCommandDiagnosticCharacters - prefix.Length);
    }

    private static void AppendDiagnosticSection(StringBuilder builder, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        builder.Append("--- ").Append(name).AppendLine(" ---");
        builder.AppendLine(value.TrimEnd());
    }

    private static string BoundTail(string value, int maximumCharacters)
        => value.Length <= maximumCharacters ? value : value[^maximumCharacters..];

    private static string ResolveSteamHomeDirectory(CloudWorkerOptions options)
    {
        var candidate = Path.GetFullPath(
            string.IsNullOrWhiteSpace(options.Arma3.SteamHomeDirectory)
                ? Path.Combine(options.RuntimeRoot, ".utf-tooling", "steam-home")
                : options.Arma3.SteamHomeDirectory);
        var runtimeRoot = Path.GetFullPath(options.RuntimeRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var runtimePrefix = runtimeRoot.EndsWith(Path.DirectorySeparatorChar)
            ? runtimeRoot
            : runtimeRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(runtimePrefix, comparison))
            throw new InvalidOperationException("SteamCMD's home directory must be inside the runtime root.");
        return candidate;
    }

    private string BuildSteamCmdScript(
        string? password,
        bool validate,
        IReadOnlyCollection<ulong> workshopItemIds)
    {
        var builder = new StringBuilder();
        builder.AppendLine("@ShutdownOnFailedCommand 1");
        builder.AppendLine("@NoPromptForPassword 1");
        builder.Append("force_install_dir ").AppendLine(QuoteSteam(_paths.InstallPath));
        builder.Append("login ").Append(QuoteSteam(_options.Arma3.SteamUsername));
        if (!string.IsNullOrWhiteSpace(password))
            builder.Append(' ').Append(QuoteSteam(password));
        builder.AppendLine();
        builder.Append("app_update ").Append(Arma3RuntimePaths.DedicatedServerAppId.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(_options.Arma3.Branch))
            builder.Append(" -beta ").Append(QuoteSteam(_options.Arma3.Branch));
        if (validate)
            builder.Append(" validate");
        builder.AppendLine();
        foreach (var workshopItemId in workshopItemIds)
        {
            builder
                .Append("workshop_download_item ")
                .Append(Arma3RuntimePaths.WorkshopAppId.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(workshopItemId.ToString(CultureInfo.InvariantCulture));
            if (validate)
                builder.Append(" validate");
            builder.AppendLine();
        }
        builder.AppendLine("quit");
        return builder.ToString();
    }

    private IReadOnlyList<string> BuildLaunchArguments(Arma3CommandPayload payload)
    {
        if (!payload.Command.IsPrimaryService)
            return BuildAuxiliaryLaunchArguments(payload);
        var arguments = new List<string>
        {
            $"-config={_paths.ServerConfigurationPath}",
            $"-cfg={_paths.BasicConfigurationPath}",
            $"-profiles={_paths.ProfilePath}",
            $"-bepath={_paths.BattlEyePath}",
            "-autoInit",
        };
        if (payload.Command.Port is { } port)
        {
            if (port is < 1024 or > 65535)
                throw new InvalidDataException("Arma game port is outside the allowed range.");
            arguments.Add($"-port={port.ToString(CultureInfo.InvariantCulture)}");
        }
        if (!string.IsNullOrWhiteSpace(payload.BindAddress))
        {
            if (!IPAddress.TryParse(payload.BindAddress, out var address))
                throw new InvalidDataException("Arma bind address is invalid.");
            arguments.Add($"-ip={address}");
        }
        if (!string.IsNullOrWhiteSpace(payload.ProfileName))
        {
            if (!SafeNamePattern().IsMatch(payload.ProfileName))
                throw new InvalidDataException("Arma profile name is invalid.");
            arguments.Add($"-name={payload.ProfileName}");
        }
        var workshopItemIds = payload.GetSteamWorkshopItemIds();
        var modPaths = workshopItemIds.Select(_paths.GetSteamWorkshopItemPath)
            .Concat(GetProviderModPaths(payload.Command.Content, isPrimaryService: true))
            .ToArray();
        if (modPaths.Length > 0)
        {
            EnsureSelectedWorkshopContentExists(workshopItemIds);
            arguments.Add($"-mod={string.Join(';', modPaths)}");
        }
        return arguments;
    }

    private IReadOnlyList<string> BuildAuxiliaryLaunchArguments(Arma3CommandPayload payload)
    {
        if (!string.Equals(payload.Command.ServiceIdentifier, "headless-client", StringComparison.Ordinal))
            throw new InvalidDataException("Arma worker was assigned an unsupported auxiliary service.");
        var primary = payload.Command.Topology.Single(q => q.IsPrimary);
        if (primary.Port is not { } port)
            throw new InvalidDataException("Arma Headless Client topology has no primary game port.");
        var arguments = new List<string>
        {
            "-client",
            $"-connect={primary.Address}",
            $"-port={port.ToString(CultureInfo.InvariantCulture)}",
            "-nosound",
            $"-name=UTFHC{(payload.Command.ServiceOrdinal + 1).ToString(CultureInfo.InvariantCulture)}",
            $"-profiles={_paths.ProfilePath}",
        };
        if (payload.Command.Configuration.TryGetValue("server.cfg@password", out var password))
        {
            if (password.ValueKind is not JsonValueKind.String)
                throw new InvalidDataException("Arma join password is invalid for the Headless Client.");
            var value = password.GetString();
            if (!string.IsNullOrEmpty(value))
                arguments.Add($"-password={value}");
        }
        var workshopItemIds = payload.GetSteamWorkshopItemIds();
        var modPaths = workshopItemIds.Select(_paths.GetSteamWorkshopItemPath)
            .Concat(GetProviderModPaths(payload.Command.Content, isPrimaryService: false))
            .ToArray();
        if (modPaths.Length > 0)
        {
            EnsureSelectedWorkshopContentExists(workshopItemIds);
            arguments.Add($"-mod={string.Join(';', modPaths)}");
        }
        return arguments;
    }

    private IEnumerable<string> GetProviderModPaths(
        IReadOnlyCollection<WorkerContentReference> references,
        bool isPrimaryService)
        => references
            .Where(reference => reference.Source is "uploaded-artifact" or "git")
            .Where(reference => isPrimaryService
                ? !string.Equals(reference.UsageRole, "ClientOnly", StringComparison.OrdinalIgnoreCase)
                : !string.Equals(reference.UsageRole, "ServerOnly", StringComparison.OrdinalIgnoreCase))
            .Select(reference => Path.Combine(_paths.InstallPath, WorkerContentPath.GetArma3ModDirectoryName(reference)));

    private async Task<WorkerCommandExecutionResult> CollectLogsAsync(CancellationToken cancellationToken)
    {
        var candidates = new[] { _paths.LogPath, _paths.ProfilePath, _paths.InstallPath }
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
            .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
            .Where(q => q.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase)
                        || q.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(4)
            .ToArray();
        if (candidates.Length == 0)
            return WorkerCommandExecutionResult.Success("logs-empty", "No Arma runtime logs are available.");
        var output = new StringBuilder();
        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Append("== ").Append(Path.GetFileName(file)).AppendLine(" ==");
            output.AppendLine(await ReadTailAsync(file, 4096, cancellationToken).ConfigureAwait(false));
        }
        return WorkerCommandExecutionResult.Success(
            "logs-collected",
            WorkerSecretRedactor.Redact(output.ToString(), SnapshotDiagnosticSecrets()));
    }

    private async Task<WorkerCommandExecutionResult> CollectLogsRestartSafeAsync(
        CancellationToken cancellationToken)
    {
        await RememberRestartSafeDiagnosticSecretsAsync(cancellationToken).ConfigureAwait(false);
        return await CollectLogsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RememberRestartSafeDiagnosticSecretsAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(_options.Arma3.SteamUsername, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            var steamPassword = await _options.Arma3.ReadSteamPasswordAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(steamPassword))
                throw new InvalidDataException("Steam diagnostic redaction material is unavailable.");
            RememberDiagnosticSecret(steamPassword);
        }

        await RememberArmaConfigurationSecretsAsync(
                _paths.ServerConfigurationPath,
                cancellationToken)
            .ConfigureAwait(false);
        await RememberArmaConfigurationSecretsAsync(
                _paths.BasicConfigurationPath,
                cancellationToken)
            .ConfigureAwait(false);
        await RememberBattlEyeConfigurationSecretsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RememberArmaConfigurationSecretsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var content = await ReadBoundedDiagnosticConfigurationAsync(path, cancellationToken).ConfigureAwait(false);
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = line[..separator].Trim();
            if (!SensitiveConfigurationKeyPattern().IsMatch(key))
                continue;
            var rawValue = line[(separator + 1)..].Trim().TrimEnd(';').Trim();
            if (rawValue.Length == 0)
                throw new InvalidDataException("A sensitive Arma configuration value cannot be redacted safely.");
            RememberDiagnosticSecret(UnquoteArmaValue(rawValue));
        }
    }

    private async Task RememberBattlEyeConfigurationSecretsAsync(CancellationToken cancellationToken)
    {
        var content = await ReadBoundedDiagnosticConfigurationAsync(
                _paths.BattlEyeConfigurationPath,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOfAny([' ', '\t']);
            if (separator <= 0)
                continue;
            var key = line[..separator].Trim();
            if (!SensitiveConfigurationKeyPattern().IsMatch(key))
                continue;
            var value = line[(separator + 1)..].Trim();
            if (value.Length == 0)
                throw new InvalidDataException("A sensitive BattlEye configuration value cannot be redacted safely.");
            RememberDiagnosticSecret(value);
        }
    }

    private static async Task<string> ReadBoundedDiagnosticConfigurationAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            throw new InvalidDataException("Rendered configuration is unavailable; fault diagnostics are suppressed.");
        var file = new FileInfo(path);
        if (file.Length > MaximumDiagnosticConfigurationBytes)
            throw new InvalidDataException("Rendered configuration is too large for safe diagnostic redaction.");
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static string UnquoteArmaValue(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal)
            : value;

    private static async Task<string> ReadTailAsync(
        string path,
        int byteCount,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > byteCount)
            stream.Seek(-byteCount, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string QuoteSteam(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private string PrepareSteamTemporaryRoot(string configuredPath)
    {
        var root = Path.GetFullPath(configuredPath);
        if (!OperatingSystem.IsWindows())
        {
            var systemTemporaryRoot = Path.GetFullPath("/tmp");
            var prefix = systemTemporaryRoot.EndsWith(Path.DirectorySeparatorChar)
                ? systemTemporaryRoot
                : systemTemporaryRoot + Path.DirectorySeparatorChar;
            if (!root.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Steam credential runscripts must be created below /tmp.");
            }
        }
        Directory.CreateDirectory(root);
        WorkerStoragePath.RejectExistingLinks(root, root);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                root,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute);
        }
        return root;
    }

    private string CreateSteamTemporaryDirectory()
    {
        var path = Path.Combine(
            _steamTemporaryRoot,
            $"{SteamTemporaryDirectoryPrefix}-{_connection.DeploymentId:N}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        WorkerStoragePath.RejectExistingLinks(_steamTemporaryRoot, path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute);
        }
        return path;
    }

    private void CleanupStaleSteamTemporaryArtifacts()
    {
        var prefix = $"{SteamTemporaryDirectoryPrefix}-{_connection.DeploymentId:N}-";
        foreach (var path in Directory.EnumerateDirectories(_steamTemporaryRoot, $"{prefix}*"))
        {
            if (IsOwnedSteamTemporaryDirectory(path, prefix))
                TryDeleteSteamTemporaryDirectory(path);
        }
        foreach (var path in Directory.EnumerateFiles(
                     _steamTemporaryRoot,
                     $"{SteamTemporaryDirectoryPrefix}-{_connection.DeploymentId:N}-*{SteamRunScriptExtension}"))
        {
            WorkerStoragePath.RejectLink(path);
            File.Delete(path);
        }
        CleanupLegacyDeploymentRunScripts();
    }

    private void CleanupLegacyDeploymentRunScripts()
    {
        if (!Directory.Exists(_paths.DeploymentRoot))
            return;
        WorkerStoragePath.RejectExistingLinks(_paths.DeploymentRoot, _paths.DeploymentRoot);
        foreach (var path in Directory.EnumerateFiles(_paths.DeploymentRoot, "steamcmd-*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(path);
            var extension = Path.GetExtension(name);
            var suffix = name["steamcmd-".Length..^extension.Length];
            if (extension is not (".txt" or SteamRunScriptExtension)
                || !Guid.TryParseExact(suffix, "N", out _))
            {
                continue;
            }
            WorkerStoragePath.RejectLink(path);
            File.Delete(path);
        }
    }

    private static bool IsOwnedSteamTemporaryDirectory(string path, string prefix)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith(prefix, StringComparison.Ordinal)
               && Guid.TryParseExact(name[prefix.Length..], "N", out _);
    }

    private static void TryDeleteSteamTemporaryDirectory(string path)
    {
        try
        {
            WorkerStoragePath.RejectLink(path);
            foreach (var file in Directory.EnumerateFiles(
                         path,
                         $"{SteamTemporaryDirectoryPrefix}-*{SteamRunScriptExtension}",
                         SearchOption.TopDirectoryOnly))
            {
                WorkerStoragePath.RejectLink(file);
                File.Delete(file);
            }
            if (!Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void RememberPayloadSecrets(Arma3CommandPayload payload)
    {
        var sensitiveKeys = GameConfigurationImplementations.Get(payload.Command.GameIdentifier)
            .Definitions
            .Where(q => q.Sensitive)
            .Select(q => q.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (key, value) in payload.Command.Configuration)
        {
            if (sensitiveKeys.Contains(key))
                RememberJsonSecrets(value);
        }
    }

    private Arma3CommandPayload AddSecretConfiguration(
        Arma3CommandPayload payload,
        IReadOnlyDictionary<string, string> secrets)
    {
        var configuration = payload.Command.Configuration.ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal);
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["join-password"] = "server.cfg@password",
            ["admin-password"] = "server.cfg@passwordAdmin",
            ["server-command-password"] = "server.cfg@serverCommandPassword",
            ["battleye-rcon-password"] = "beserver.cfg@RConPassword",
        };
        foreach (var (secretKey, configurationKey) in mappings)
        {
            if (!secrets.TryGetValue(secretKey, out var value)) continue;
            RememberDiagnosticSecret(value);
            configuration[configurationKey] = JsonSerializer.SerializeToElement(value);
        }
        return payload with { Command = payload.Command with { Configuration = configuration } };
    }

    private void RememberJsonSecrets(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.String)
        {
            RememberDiagnosticSecret(value.GetString());
            return;
        }
        if (value.ValueKind is not JsonValueKind.Array)
            return;
        foreach (var item in value.EnumerateArray())
            RememberJsonSecrets(item);
    }

    private void RememberDiagnosticSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length > 4096)
            return;
        lock (_diagnosticSecretsLock)
            _diagnosticSecrets.Add(secret);
    }

    private string[] SnapshotDiagnosticSecrets()
    {
        lock (_diagnosticSecretsLock)
            return _diagnosticSecrets.ToArray();
    }

    private static TimeSpan StopTimeout(Arma3CommandPayload payload)
        => TimeSpan.FromSeconds(payload.Command.GracefulStopTimeoutSeconds);

    private (long? TotalBytes, long? AvailableBytes, long? ControlledBytes) ObserveDeploymentStorage()
    {
        try
        {
            var storagePath = Path.GetFullPath(_paths.DeploymentRoot);
            while (!Directory.Exists(storagePath))
            {
                var parent = Directory.GetParent(storagePath);
                if (parent is null)
                    return (null, null, null);
                storagePath = parent.FullName;
            }
            var drive = new DriveInfo(storagePath);
            if (!drive.IsReady)
                return (null, null, null);
            var totalBytes = drive.TotalSize;
            var availableBytes = drive.AvailableFreeSpace;
            var controlledBytes = Directory.Exists(_paths.DeploymentRoot)
                ? MeasureDirectory(_paths.DeploymentRoot)
                : 0;
            return totalBytes >= 0 && availableBytes >= 0 && availableBytes <= totalBytes
                ? (totalBytes, availableBytes, controlledBytes)
                : (null, null, null);
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or ArgumentException
                                       or NotSupportedException)
        {
            return (null, null, null);
        }
    }

    private WorkerCommandExecutionResult? ValidateStorageCapacity(Arma3CommandPayload payload)
    {
        if (payload.Command.PeakStorageRequiredBytes is not { } requiredBytes)
            return null;
        var storage = ObserveDeploymentStorage();
        if (storage.AvailableBytes is not { } availableBytes || storage.ControlledBytes is not { } controlledBytes)
        {
            return WorkerCommandExecutionResult.Failure(
                "storage-capacity-unavailable",
                "storage-capacity-unavailable",
                "Mounted deployment storage could not be measured safely before SteamCMD.");
        }
        var usableBytes = availableBytes > long.MaxValue - controlledBytes
            ? long.MaxValue
            : availableBytes + controlledBytes;
        return usableBytes >= requiredBytes
            ? null
            : WorkerCommandExecutionResult.Failure(
                "storage-capacity-insufficient",
                "storage-capacity-insufficient",
                $"Mounted deployment storage provides {usableBytes} usable bytes but preparation requires "
                + $"{requiredBytes} bytes. Select a larger profile or attach external storage.");
    }

    private IReadOnlyCollection<WorkerStorageMeasurement> MeasurePreparedContent(
        IReadOnlyCollection<ulong> workshopItemIds)
    {
        var measurements = new List<WorkerStorageMeasurement>(workshopItemIds.Count + 1)
        {
            new()
            {
                ComponentKind = "runtime",
                ApplicationIdentifier = Arma3RuntimePaths.DedicatedServerAppId.ToString(CultureInfo.InvariantCulture),
                ItemIdentifier = "dedicated-server",
                InstalledBytes = MeasureDirectory(_paths.InstallPath, [_paths.SteamWorkshopContentPath]),
                VersionMetadata = ReadManifestValue(_paths.RuntimeManifestPath, "buildid"),
            },
        };
        foreach (var workshopItemId in workshopItemIds.Distinct().Order())
        {
            measurements.Add(new WorkerStorageMeasurement
            {
                ComponentKind = "workshop",
                ApplicationIdentifier = Arma3RuntimePaths.WorkshopAppId.ToString(CultureInfo.InvariantCulture),
                ItemIdentifier = workshopItemId.ToString(CultureInfo.InvariantCulture),
                InstalledBytes = MeasureDirectory(_paths.GetSteamWorkshopItemPath(workshopItemId)),
                VersionMetadata = ReadWorkshopManifestVersion(workshopItemId),
            });
        }
        return measurements;
    }

    private static long MeasureDirectory(string root, IReadOnlyCollection<string>? excludedRoots = null)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("A prepared content path is unavailable for measurement.");
        WorkerStoragePath.RejectExistingLinks(root, root);
        var excluded = (excludedRoots ?? [])
            .Select(Path.GetFullPath)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            if (excluded.Contains(directory))
                continue;
            WorkerStoragePath.RejectLink(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                WorkerStoragePath.RejectLink(entry);
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(Path.GetFullPath(entry));
                    continue;
                }
                var length = new FileInfo(entry).Length;
                total = checked(total + length);
            }
        }
        return total;
    }

    private static string? ReadManifestValue(string path, string key)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024 * 1024)
            return null;
        WorkerStoragePath.RejectLink(path);
        var content = File.ReadAllText(path);
        var match = Regex.Match(
            content,
            $"\\\"{Regex.Escape(key)}\\\"\\s*\\\"(?<value>[^\\\"]+)\\\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private string? ReadWorkshopManifestVersion(ulong workshopItemId)
    {
        if (!File.Exists(_paths.WorkshopManifestPath)
            || new FileInfo(_paths.WorkshopManifestPath).Length > 16 * 1024 * 1024)
        {
            return null;
        }
        WorkerStoragePath.RejectLink(_paths.WorkshopManifestPath);
        var content = File.ReadAllText(_paths.WorkshopManifestPath);
        var item = Regex.Match(
            content,
            $"\\\"{workshopItemId.ToString(CultureInfo.InvariantCulture)}\\\"\\s*\\{{(?<body>.*?)\\n\\s*\\}}",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (!item.Success)
            return null;
        var version = Regex.Match(
            item.Groups["body"].Value,
            "\\\"timeupdated\\\"\\s*\\\"(?<value>[0-9]+)\\\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return version.Success ? version.Groups["value"].Value : null;
    }

    private IReadOnlyCollection<WorkerObservedContentVersion> ObserveWorkshopVersions(
        IReadOnlyCollection<WorkerContentReference> references,
        bool isPrimaryService)
    {
        var observed = new List<WorkerObservedContentVersion>();
        foreach (var reference in references.Where(
                     reference => reference.Source == Arma3CommandPayload.SteamWorkshopContentSource)
                     .Where(reference => payloadRoleApplies(reference)))
        {
            if (!ulong.TryParse(reference.Identifier, out var workshopItemId))
            {
                throw new InvalidDataException("Steam Workshop version evidence is unavailable after preparation.");
            }
            var version = ReadWorkshopManifestVersion(workshopItemId);
            if (string.IsNullOrWhiteSpace(version))
            {
                var contentPath = _paths.GetSteamWorkshopItemPath(workshopItemId);
                if (!Directory.Exists(contentPath))
                    throw new InvalidDataException("Steam Workshop version evidence is unavailable after preparation.");
                version = Directory.GetLastWriteTimeUtc(contentPath).Ticks.ToString(CultureInfo.InvariantCulture);
            }
            observed.Add(new WorkerObservedContentVersion
            {
                ProviderIdentifier = reference.Source,
                SourceIdentifier = reference.Identifier,
                ObservedVersion = version,
            });
        }
        return observed;

        bool payloadRoleApplies(WorkerContentReference reference)
            => isPrimaryService
                ? !string.Equals(reference.UsageRole, "ClientOnly", StringComparison.OrdinalIgnoreCase)
                : !string.Equals(reference.UsageRole, "ServerOnly", StringComparison.OrdinalIgnoreCase);
    }

    private bool SelectedWorkshopContentExists(IEnumerable<ulong> workshopItemIds)
    {
        foreach (var workshopItemId in workshopItemIds)
        {
            var path = _paths.GetSteamWorkshopItemPath(workshopItemId);
            if (!Directory.Exists(path))
                return false;
            WorkerStoragePath.RejectExistingLinks(_paths.DeploymentRoot, path);
        }
        return true;
    }

    private void EnsureSelectedWorkshopContentExists(IEnumerable<ulong> workshopItemIds)
    {
        if (!SelectedWorkshopContentExists(workshopItemIds))
        {
            throw new InvalidDataException(
                "Selected Arma Workshop content is unavailable after SteamCMD preparation.");
        }
    }

    private bool IsPrepared()
        => File.Exists(_paths.ExecutablePath) && File.Exists(_paths.PreparationMarkerPath);

    private async Task WritePreparationMarkerAsync(CancellationToken cancellationToken)
    {
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(
                _paths.DeploymentRoot,
                ".utf-arma3-prepared",
                ".tmp",
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, _paths.PreparationMarkerPath, true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                _paths.PreparationMarkerPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static bool IsOriginalProcess(
        WorkerCommandJournalEntry journalEntry,
        Arma3ProcessIdentity observedIdentity)
        => journalEntry.ProcessIdBeforeExecution == observedIdentity.ProcessId
           && journalEntry.ProcessStartedAtBeforeExecution == observedIdentity.ProcessStartedAt;

    private static bool IsStorageTransferFailure(Exception exception)
        => exception is HttpRequestException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or JsonException
            or CryptographicException
            or OverflowException;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeNamePattern();

    [GeneratedRegex(
        "(?:password|secret|token|credential|api[_-]?key|private[_-]?key)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveConfigurationKeyPattern();
}
