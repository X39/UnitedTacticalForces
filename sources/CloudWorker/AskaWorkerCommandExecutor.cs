using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Executes ASKA lifecycle commands inside one dedicated external worker.</summary>
public sealed partial class AskaWorkerCommandExecutor : IWorkerCommandExecutor
{
    private readonly CloudWorkerConnectionConfig _connection;
    private readonly CloudWorkerOptions _options;
    private readonly AskaRuntimePaths _paths;
    private readonly IProcessCommandRunner _runner;
    private readonly IAskaProcessController _process;
    private readonly ICloudWorkerStorageTransfer _storage;
    private readonly RestrictedGameProcessLauncher _launcher;
    private readonly AskaConfigurationRenderer _configurationRenderer;

    /// <summary>Initializes an ASKA executor and its deployment-bound collaborators.</summary>
    /// <param name="connection">Deployment-scoped worker connection.</param>
    /// <param name="options">Worker runtime options.</param>
    /// <param name="runner">Optional command runner for tests.</param>
    /// <param name="process">Optional process controller for tests.</param>
    /// <param name="storage">Optional storage transfer for tests.</param>
    /// <param name="configurationRenderer">Optional properties renderer for tests.</param>
    public AskaWorkerCommandExecutor(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        IProcessCommandRunner? runner = null,
        IAskaProcessController? process = null,
        ICloudWorkerStorageTransfer? storage = null,
        AskaConfigurationRenderer? configurationRenderer = null)
    {
        _connection = connection;
        _options = options;
        _paths = new AskaRuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.Aska.ExecutableRelativePath);
        _runner = runner ?? new ProcessCommandRunner();
        _launcher = new RestrictedGameProcessLauncher(new RestrictedProcessOptions(
            options.SteamCmd.GameUserId,
            options.SteamCmd.GameGroupId,
            options.SteamCmd.PrivilegeDropPath,
            options.SteamCmd.SteamUserId));
        _process = process ?? new AskaProcessController(
            connection.ServerId, connection.DeploymentId, _paths, options.Aska, _launcher);
        _storage = storage ?? new UnavailableCloudWorkerStorageTransfer();
        _configurationRenderer = configurationRenderer ?? new AskaConfigurationRenderer();
    }

    /// <summary>Gets the runtime log reader, which tests may replace to reproduce diagnostic read failures.</summary>
    internal Func<string, CancellationToken, Task<string>> LogReader { get; init; }
        = (path, token) => File.ReadAllTextAsync(path, token);

    /// <inheritdoc />
    public async Task<WorkerCommandExecutionResult> ExecuteAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        try
        {
            command.Validate(_connection);
            var payload = AskaCommandPayload.Parse(command.Payload);
            payload.Validate(_connection, command);
            return command.ActionIdentifier.Trim().ToLowerInvariant() switch
            {
                "install" => await MaintainAsync(false, cancellationToken).ConfigureAwait(false),
                "update" => await MaintainAsync(true, cancellationToken).ConfigureAwait(false),
                "start" => await StartAsync(command, payload, true, cancellationToken).ConfigureAwait(false),
                "restart" => await RestartAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "stop" => await StopAndPreserveAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "fault-stop" => await FaultStopAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "terminate" => await TerminateAsync(cancellationToken).ConfigureAwait(false),
                "collect-logs" => await CollectLogsAsync(command.Secrets.Values, cancellationToken).ConfigureAwait(false),
                _ => WorkerCommandExecutionResult.Failure(
                    "action-unsupported", "unsupported-action", "The ASKA worker action is unsupported."),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidDataException or InvalidOperationException
                                              or ArgumentException)
        {
            return WorkerCommandExecutionResult.Failure(
                "worker-execution-failed",
                "worker-operation-failed",
                WorkerSecretRedactor.Redact(exception.Message, command.Secrets.Values)
                ?? "ASKA worker operation failed.");
        }
    }

    /// <inheritdoc />
    public async Task<WorkerCommandExecutionResult?> ReconcileRedeliveryAsync(
        WorkerCommandClaimResponse command,
        WorkerCommandJournalEntry journalEntry,
        CancellationToken cancellationToken)
    {
        var payload = AskaCommandPayload.Parse(command.Payload);
        payload.Validate(_connection, command);
        var action = command.ActionIdentifier.Trim().ToLowerInvariant();
        if (action is "fault-stop"
            && payload.Command.FaultEvidenceDeadline <= DateTimeOffset.UtcNow)
        {
            return WorkerCommandExecutionResult.Failure(
                "fault-evidence-window-expired",
                "fault-evidence-deadline-expired",
                "The persisted fault-evidence deadline expired before redelivery reconciliation.");
        }
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
            return WorkerCommandExecutionResult.Failure(
                "redelivery-reconcile-failed", "process-identity-invalid", observation.Error);
        if (action is "start" && observation.IsRunning && observation.Identity is { Ready: true } identity)
            return WorkerCommandExecutionResult.Success(
                "reconciled-running", processId: identity.ProcessId, processStartedAt: identity.ProcessStartedAt);
        if (action is "stop" && !observation.IsRunning && !payload.Command.PreserveOnStop)
        {
            _configurationRenderer.DeleteRuntimeConfiguration(_paths);
            return WorkerCommandExecutionResult.Success("reconciled-stopped");
        }
        if (action is "terminate" && !observation.IsRunning)
        {
            _configurationRenderer.DeleteRuntimeConfiguration(_paths);
            return WorkerCommandExecutionResult.Success("reconciled-stopped");
        }
        return null;
    }

    /// <inheritdoc />
    public async Task CompensateSupersededAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        if (command.ActionIdentifier.Trim().ToLowerInvariant() is "start" or "restart" or "install" or "update")
            _ = await _process.TerminateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken)
    {
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        var root = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_paths.DeploymentRoot))!);
        var status = observation.Error is not null
            ? "Degraded"
            : observation.IsRunning
                ? observation.Identity?.Ready is true ? "Running" : "Starting"
                : IsPrepared() ? "Prepared" : "Unprepared";
        return new WorkerRuntimeStatus(
            status,
            observation.Identity?.ProcessId,
            observation.Identity?.ProcessStartedAt,
            observation.Error,
            root.TotalSize,
            root.AvailableFreeSpace,
            Directory.Exists(_paths.DeploymentRoot) ? Measure(_paths.DeploymentRoot) : 0);
    }

    private async Task<WorkerCommandExecutionResult> StartAsync(
        WorkerCommandClaimResponse command,
        AskaCommandPayload payload,
        bool restoreSnapshot,
        CancellationToken cancellationToken)
    {
        using var current = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (current.Error is not null)
            return WorkerCommandExecutionResult.Failure("start-reconcile-failed", "process-identity-invalid", current.Error);
        if (current.IsRunning && current.Identity is not null)
            return WorkerCommandExecutionResult.Success(
                "already-running", processId: current.Identity.ProcessId, processStartedAt: current.Identity.ProcessStartedAt);
        if (!IsPrepared())
        {
            var installed = await InstallAsync(true, cancellationToken).ConfigureAwait(false);
            if (!installed.Succeeded)
                return installed with { Stage = "start-prepare-failed" };
        }
        if (restoreSnapshot)
        {
            await _storage.ResetPersistentOverlayAsync(_paths.DeploymentRoot, cancellationToken).ConfigureAwait(false);
            await _storage.ApplyPersistentSnapshotAsync(command, _paths.DeploymentRoot, cancellationToken).ConfigureAwait(false);
        }
        await _configurationRenderer.RenderAsync(
            _paths, payload.Command.Configuration, command.Secrets, cancellationToken).ConfigureAwait(false);
        return await _process.StartAsync(command.Secrets.Values, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerCommandExecutionResult> RestartAsync(
        WorkerCommandClaimResponse command,
        AskaCommandPayload payload,
        CancellationToken cancellationToken)
    {
        var stopped = await _process.StopAsync(
            false, StopTimeout(payload), payload.Command.GracefulStopAttempts, cancellationToken).ConfigureAwait(false);
        if (!stopped.Succeeded)
            return stopped with { Stage = "restart-stop-failed" };
        await _configurationRenderer.SynchronizeSaveIdAsync(_paths, cancellationToken).ConfigureAwait(false);
        var started = await StartAsync(command, payload, false, cancellationToken).ConfigureAwait(false);
        return started with { Stage = started.Succeeded ? "restarted" : "restart-start-failed" };
    }

    private async Task<WorkerCommandExecutionResult> StopAndPreserveAsync(
        WorkerCommandClaimResponse command,
        AskaCommandPayload payload,
        CancellationToken cancellationToken)
    {
        var stopped = await _process.StopAsync(
            false, StopTimeout(payload), payload.Command.GracefulStopAttempts, cancellationToken).ConfigureAwait(false);
        if (!stopped.Succeeded)
            return stopped;
        if (!payload.Command.PreserveOnStop)
        {
            _configurationRenderer.DeleteRuntimeConfiguration(_paths);
            return stopped;
        }
        try
        {
            await _configurationRenderer.SynchronizeSaveIdAsync(_paths, cancellationToken).ConfigureAwait(false);
            await _storage.PublishPreservationAsync(command, _paths.DeploymentRoot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _configurationRenderer.DeleteRuntimeConfiguration(_paths);
        }
        return stopped with
        {
            Output = $"ProcessStopOutcome={stopped.Stage}; PreservationConsistency="
                     + (stopped.Stage == "force-stopped" ? "Potentially inconsistent" : "Consistent"),
        };
    }

    private async Task<WorkerCommandExecutionResult> FaultStopAsync(
        WorkerCommandClaimResponse command,
        AskaCommandPayload payload,
        CancellationToken cancellationToken)
    {
        var deadline = payload.Command.FaultEvidenceDeadline
                       ?? throw new InvalidDataException("Fault stop has no evidence deadline.");
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return WorkerCommandExecutionResult.Failure(
                "fault-evidence-window-expired",
                "fault-evidence-deadline-expired",
                "The bounded fault-evidence deadline expired before worker execution began.");
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(remaining);
        var diagnostics = "Fault diagnostics unavailable.";
        try
        {
            var logs = await CollectLogsAsync(command.Secrets.Values, bounded.Token).ConfigureAwait(false);
            diagnostics = logs.Output ?? logs.Stage;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                                  or InvalidDataException)
        {
            diagnostics = "Fault diagnostics unavailable: "
                          + (WorkerSecretRedactor.Redact(exception.Message, command.Secrets.Values)
                             ?? "log collection failed");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                  && bounded.IsCancellationRequested)
        {
            return WorkerCommandExecutionResult.Failure(
                "fault-evidence-window-expired",
                "fault-evidence-deadline-expired",
                "The bounded fault-evidence deadline expired; provider teardown may continue.") with
            {
                Output = diagnostics,
            };
        }

        try
        {
            var stopped = await StopAndPreserveAsync(command, payload, bounded.Token).ConfigureAwait(false);
            return stopped with { Output = $"{diagnostics}\n{stopped.Output}" };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                  && bounded.IsCancellationRequested)
        {
            return WorkerCommandExecutionResult.Failure(
                "fault-evidence-window-expired",
                "fault-evidence-deadline-expired",
                "The bounded fault-evidence deadline expired; provider teardown may continue.") with
            {
                Output = diagnostics,
            };
        }
    }

    private async Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
    {
        var result = await _process.TerminateAsync(cancellationToken).ConfigureAwait(false);
        _configurationRenderer.DeleteRuntimeConfiguration(_paths);
        return result;
    }

    private async Task<WorkerCommandExecutionResult> MaintainAsync(
        bool validate,
        CancellationToken cancellationToken)
    {
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.IsRunning)
            return WorkerCommandExecutionResult.Failure(
                "maintenance-running", "game-running", "Stop ASKA before installing or updating it.");
        return await InstallAsync(validate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerCommandExecutionResult> InstallAsync(
        bool validate,
        CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        if (File.Exists(_paths.PreparationMarkerPath))
            File.Delete(_paths.PreparationMarkerPath);
        var script = BuildSteamScript(validate);
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            _options.SteamCmd.TemporaryDirectory, "utf-aska-steamcmd", ".runscript", script, cancellationToken)
            .ConfigureAwait(false);
        if (_options.SteamCmd.SteamUserId is { } steamUserId)
            temporary.RestrictToUnixOwner(steamUserId);
        var steam = _launcher.CreateSteamCmd(
            _options.SteamCmd.SteamCmdPath,
            _paths.InstallPath,
            ["+runscript", temporary.Path]);
        steam.Environment["HOME"] = _options.SteamCmd.SteamHomeDirectory ?? _paths.InstallPath;
        var result = await _runner.RunAsync(steam, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || !File.Exists(_paths.ExecutablePath))
            return WorkerCommandExecutionResult.Failure(
                "steamcmd-failed", "aska-install-failed", result.Error ?? "SteamCMD failed.");

        var wineBoot = _launcher.Create(
            _options.Aska.XvfbRunPath,
            _paths.DeploymentRoot,
            ["-a", "--server-args=-screen 0 1024x768x24 -nolisten tcp", _options.Aska.WineBootPath, "--init"]);
        wineBoot.Environment["WINEPREFIX"] = _paths.WinePrefixPath;
        wineBoot.Environment["WINEARCH"] = "win64";
        wineBoot.Environment["WINEDEBUG"] = "-all";
        wineBoot.Environment["HOME"] = _paths.DeploymentRoot;
        using var initialization = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        initialization.CancelAfter(TimeSpan.FromMinutes(5));
        ProcessCommandResult initialized;
        try
        {
            initialized = await _runner.RunAsync(wineBoot, initialization.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WorkerCommandExecutionResult.Failure(
                "wine-prefix-timeout", "aska-wine-prefix-timeout", "Wine prefix initialization timed out.");
        }
        if (!initialized.Succeeded || !Directory.Exists(_paths.WinePrefixPath))
            return WorkerCommandExecutionResult.Failure(
                "wine-prefix-failed", "aska-wine-prefix-failed", initialized.Error ?? "Wine prefix initialization failed.");

        await File.WriteAllTextAsync(
            _paths.PreparationMarkerPath,
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);
        return WorkerCommandExecutionResult.Success(validate ? "validated" : "installed") with
        {
            StorageMeasurements =
            [
                new WorkerStorageMeasurement
                {
                    ComponentKind = "runtime",
                    ApplicationIdentifier = AskaRuntimePaths.ApplicationId.ToString(CultureInfo.InvariantCulture),
                    ItemIdentifier = "dedicated-server",
                    InstalledBytes = Measure(_paths.InstallPath),
                    VersionMetadata = ReadManifestBuildId(),
                },
            ],
        };
    }

    private async Task<WorkerCommandExecutionResult> CollectLogsAsync(
        IEnumerable<string> secrets,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.RuntimeLogPath))
            return WorkerCommandExecutionResult.Success("logs-empty", "No ASKA logs are available.");
        var text = await LogReader(_paths.RuntimeLogPath, cancellationToken).ConfigureAwait(false);
        if (text.Length > 16 * 1024)
            text = text[^16384..];
        return WorkerCommandExecutionResult.Success(
            "logs-collected", WorkerSecretRedactor.Redact(text, secrets));
    }

    private string BuildSteamScript(bool validate)
    {
        var builder = new StringBuilder("@ShutdownOnFailedCommand 1\n@NoPromptForPassword 1\n");
        builder.Append("@sSteamCmdForcePlatformType windows\nforce_install_dir ")
            .AppendLine(QuoteSteam(_paths.InstallPath));
        builder.Append("login anonymous\napp_update 3246670");
        if (validate)
            builder.Append(" validate");
        return builder.Append("\nquit\n").ToString();
    }

    private bool IsPrepared()
        => File.Exists(_paths.ExecutablePath)
           && File.Exists(_paths.PreparationMarkerPath)
           && File.Exists(Path.Combine(_paths.WinePrefixPath, "system.reg"));

    private static TimeSpan StopTimeout(AskaCommandPayload payload)
        => TimeSpan.FromSeconds(payload.Command.GracefulStopTimeoutSeconds);

    private static string QuoteSteam(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private string? ReadManifestBuildId()
    {
        if (!File.Exists(_paths.RuntimeManifestPath))
            return null;
        var match = BuildIdPattern().Match(File.ReadAllText(_paths.RuntimeManifestPath));
        return match.Success ? match.Groups[1].Value : null;
    }

    private static long Measure(string root)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Storage measurement refuses linked files.");
            total = checked(total + new FileInfo(file).Length);
        }
        return total;
    }

    [GeneratedRegex("\\\"buildid\\\"\\s*\\\"([0-9]+)\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuildIdPattern();
}
