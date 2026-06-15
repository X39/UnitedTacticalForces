using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Minimal native Arma 3 worker implementation. Provider concerns never enter this type.
/// </summary>
public sealed partial class Arma3WorkerCommandExecutor : IWorkerCommandExecutor
{
    private const string SteamTemporaryDirectoryPrefix = "utf-steamcmd";
    private const string SteamRunScriptExtension = ".runscript";
    private const int MaximumDiagnosticConfigurationBytes = 1024 * 1024;
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
        _steamTemporaryRoot = PrepareSteamTemporaryRoot(options.Arma3.TemporaryDirectory);
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
                "restart" => await RestartAsync(payload, cancellationToken).ConfigureAwait(false),
                "collect-logs" => await CollectLogsRestartSafeAsync(cancellationToken).ConfigureAwait(false),
                _ => WorkerCommandExecutionResult.Failure(
                    "action-unsupported",
                    "unsupported-action",
                    "The requested worker action is not supported."),
            };
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
        using var observation = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
            return new WorkerRuntimeStatus("identity-invalid", null, null, observation.Error);
        return observation.IsRunning && observation.Identity is not null
            ? new WorkerRuntimeStatus(
                "running",
                observation.Identity.ProcessId,
                observation.Identity.ProcessStartedAt,
                null)
            : new WorkerRuntimeStatus(
                IsPrepared() ? "prepared" : "not-installed",
                null,
                null,
                null);
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
        if (!IsPrepared())
        {
            var prepared = await InstallAsync(payload.Validate, cancellationToken).ConfigureAwait(false);
            if (!prepared.Succeeded)
                return prepared with { Stage = "start-prepare-failed" };
        }
        try
        {
            await _storageTransfer
                .ResetPersistentOverlayAsync(_paths.DeploymentRoot, cancellationToken)
                .ConfigureAwait(false);
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
            await _storageTransfer
                .ApplyPersistentSnapshotAsync(command, _paths.DeploymentRoot, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStorageTransferFailure(ex))
        {
            return WorkerCommandExecutionResult.Failure(
                "start-persistent-overlay-failed",
                "persistent-overlay-failed",
                WorkerSecretRedactor.Redact(ex.Message, SnapshotDiagnosticSecrets())
                ?? "Persistent data could not be prepared.");
        }
        return await _processController
            .StartAsync(BuildLaunchArguments(payload), cancellationToken)
            .ConfigureAwait(false);
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
        var started = await StartAsync(payload, cancellationToken).ConfigureAwait(false);
        return started with { Stage = started.Succeeded ? "restarted" : "restart-start-failed" };
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
        var maintained = await InstallAsync(validate, cancellationToken).ConfigureAwait(false);
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
        bool validate,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.Arma3.SteamCmdPath))
        {
            return WorkerCommandExecutionResult.Failure(
                "steamcmd-unavailable",
                "steamcmd-not-found",
                "SteamCMD is not installed in the worker image.");
        }
        _paths.EnsureDirectories();
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

        var script = BuildSteamCmdScript(password, validate);
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
            await WritePreparationMarkerAsync(cancellationToken).ConfigureAwait(false);
            return WorkerCommandExecutionResult.Success(validate ? "updated" : "installed", output);
        }
        return WorkerCommandExecutionResult.Failure(
            "steamcmd-failed",
            "steamcmd-failed",
            string.IsNullOrWhiteSpace(error)
                ? $"SteamCMD exited with code {result.ExitCode}."
                : error);
    }

    private string BuildSteamCmdScript(string? password, bool validate)
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
        builder.AppendLine().AppendLine("quit");
        return builder.ToString();
    }

    private IReadOnlyList<string> BuildLaunchArguments(Arma3CommandPayload payload)
    {
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
        return arguments;
    }

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
        foreach (var (key, value) in payload.ServerConfiguration.Concat(payload.BasicConfiguration))
        {
            if (SensitiveConfigurationKeyPattern().IsMatch(key))
                RememberJsonSecrets(value);
        }
        foreach (var (key, value) in payload.BattlEyeConfiguration)
        {
            if (SensitiveConfigurationKeyPattern().IsMatch(key))
                RememberDiagnosticSecret(value);
        }
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
