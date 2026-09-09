using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed partial class CounterStrike2WorkerCommandExecutor : IWorkerCommandExecutor
{
    private readonly CloudWorkerConnectionConfig _connection;
    private readonly CloudWorkerOptions _options;
    private readonly CounterStrike2RuntimePaths _paths;
    private readonly IProcessCommandRunner _runner;
    private readonly ICounterStrike2ProcessController _process;
    private readonly ICloudWorkerStorageTransfer _storage;
    private readonly RestrictedGameProcessLauncher _launcher;
    private readonly ICounterStrike2RconClient _rcon;
    private readonly WorkerContentAcquisitionService _contentAcquisition;

    public CounterStrike2WorkerCommandExecutor(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        IProcessCommandRunner? runner = null,
        ICounterStrike2ProcessController? process = null,
        ICloudWorkerStorageTransfer? storage = null,
        ICounterStrike2RconClient? rcon = null)
    {
        _connection = connection;
        _options = options;
        _paths = new CounterStrike2RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.CounterStrike2.ExecutableRelativePath);
        _runner = runner ?? new ProcessCommandRunner();
        _launcher = new RestrictedGameProcessLauncher(new RestrictedProcessOptions(
            options.SteamCmd.GameUserId,
            options.SteamCmd.GameGroupId,
            options.SteamCmd.PrivilegeDropPath,
            options.SteamCmd.SteamUserId));
        _process = process ?? new CounterStrike2ProcessController(
            connection.ServerId, connection.DeploymentId, _paths, _launcher);
        _storage = storage ?? new UnavailableCloudWorkerStorageTransfer();
        _rcon = rcon ?? new CounterStrike2RconClient();
        _contentAcquisition = new WorkerContentAcquisitionService(options, _storage, _runner);
    }

    public async Task<WorkerCommandExecutionResult> ExecuteAsync(
        WorkerCommandClaimResponse command,
        CancellationToken cancellationToken)
    {
        try
        {
            command.Validate(_connection);
            var payload = CounterStrike2CommandPayload.Parse(command.Payload);
            payload.Validate(_connection, command);
            return command.ActionIdentifier.Trim().ToLowerInvariant() switch
            {
                "install" => await MaintainAsync(false, cancellationToken).ConfigureAwait(false),
                "update" => await MaintainAsync(true, cancellationToken).ConfigureAwait(false),
                "start" => await StartAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "restart" => await RestartAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "stop" => await StopAndPreserveAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "fault-stop" => await FaultStopAsync(command, payload, cancellationToken).ConfigureAwait(false),
                "terminate" => await _process.TerminateAsync(cancellationToken).ConfigureAwait(false),
                "collect-logs" => await CollectLogsAsync(command.Secrets.Values, cancellationToken).ConfigureAwait(false),
                _ => WorkerCommandExecutionResult.Failure("action-unsupported", "unsupported-action", "The CS2 worker action is unsupported."),
            };
        }
        catch (ContentAcquisitionException exception)
        {
            return WorkerCommandExecutionResult.Failure(
                "content-acquisition-failed",
                exception.Retryable ? $"retryable-{exception.ErrorCode}" : exception.ErrorCode,
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            return WorkerCommandExecutionResult.Failure(
                "worker-execution-failed",
                "worker-operation-failed",
                WorkerSecretRedactor.Redact(exception.Message, command.Secrets.Values) ?? "CS2 worker operation failed.");
        }
    }

    public async Task<WorkerCommandExecutionResult?> ReconcileRedeliveryAsync(
        WorkerCommandClaimResponse command,
        WorkerCommandJournalEntry journalEntry,
        CancellationToken cancellationToken)
    {
        var payload = CounterStrike2CommandPayload.Parse(command.Payload);
        payload.Validate(_connection, command);
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
            return WorkerCommandExecutionResult.Failure("redelivery-reconcile-failed", "process-identity-invalid", observation.Error);
        var action = command.ActionIdentifier.Trim().ToLowerInvariant();
        if (action is "start" && observation.IsRunning && observation.Identity is not null)
            return WorkerCommandExecutionResult.Success("reconciled-running", processId: observation.Identity.ProcessId, processStartedAt: observation.Identity.ProcessStartedAt);
        if (action is "stop" or "terminate" or "fault-stop" && !observation.IsRunning)
            return WorkerCommandExecutionResult.Success("reconciled-stopped");
        return null;
    }

    public async Task CompensateSupersededAsync(WorkerCommandClaimResponse command, CancellationToken cancellationToken)
    {
        if (command.ActionIdentifier.Trim().ToLowerInvariant() is "start" or "restart" or "install" or "update")
            _ = await _process.TerminateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkerRuntimeStatus?> ObserveRuntimeAsync(CancellationToken cancellationToken)
    {
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        var root = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_paths.DeploymentRoot))!);
        return new WorkerRuntimeStatus(
            observation.Error is null ? observation.IsRunning ? "Running" : IsPrepared() ? "Prepared" : "Unprepared" : "Degraded",
            observation.Identity?.ProcessId,
            observation.Identity?.ProcessStartedAt,
            observation.Error,
            root.TotalSize,
            root.AvailableFreeSpace,
            Directory.Exists(_paths.DeploymentRoot) ? Measure(_paths.DeploymentRoot) : 0);
    }

    private async Task<WorkerCommandExecutionResult> StartAsync(
        WorkerCommandClaimResponse command,
        CounterStrike2CommandPayload payload,
        CancellationToken cancellationToken)
    {
        using var current = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (current.Error is not null)
            return WorkerCommandExecutionResult.Failure("start-reconcile-failed", "process-identity-invalid", current.Error);
        if (current.IsRunning && current.Identity is not null)
            return WorkerCommandExecutionResult.Success("already-running", processId: current.Identity.ProcessId, processStartedAt: current.Identity.ProcessStartedAt);
        if (!IsPrepared())
        {
            var installed = await InstallAsync(true, cancellationToken).ConfigureAwait(false);
            if (!installed.Succeeded) return installed with { Stage = "start-prepare-failed" };
        }
        RequireSecrets(command);
        await _storage.ResetPersistentOverlayAsync(_paths.DeploymentRoot, cancellationToken).ConfigureAwait(false);
        await RenderConfigurationAsync(payload, command.Secrets, cancellationToken).ConfigureAwait(false);
        await _storage.ApplyPersistentSnapshotAsync(command, _paths.DeploymentRoot, cancellationToken).ConfigureAwait(false);
        var observedVersions = await _contentAcquisition
            .AcquireAndInstallAsync(command, payload.Command.Content, "cs2", _paths.InstallPath, cancellationToken)
            .ConfigureAwait(false);
        var started = await _process.StartAsync(BuildArguments(payload, command.Secrets), cancellationToken).ConfigureAwait(false);
        return started with { ObservedContentVersions = observedVersions };
    }

    private async Task<WorkerCommandExecutionResult> RestartAsync(
        WorkerCommandClaimResponse command,
        CounterStrike2CommandPayload payload,
        CancellationToken cancellationToken)
    {
        await RequestGracefulQuitAsync(command, payload, cancellationToken).ConfigureAwait(false);
        var stopped = await _process.StopAsync(false, StopTimeout(payload), payload.Command.GracefulStopAttempts, cancellationToken).ConfigureAwait(false);
        if (!stopped.Succeeded) return stopped with { Stage = "restart-stop-failed" };
        var started = await StartAsync(command, payload, cancellationToken).ConfigureAwait(false);
        return started with { Stage = started.Succeeded ? "restarted" : "restart-start-failed" };
    }

    private async Task<WorkerCommandExecutionResult> StopAndPreserveAsync(
        WorkerCommandClaimResponse command,
        CounterStrike2CommandPayload payload,
        CancellationToken cancellationToken)
    {
        await RequestGracefulQuitAsync(command, payload, cancellationToken).ConfigureAwait(false);
        var stopped = await _process.StopAsync(false, StopTimeout(payload), payload.Command.GracefulStopAttempts, cancellationToken).ConfigureAwait(false);
        if (!stopped.Succeeded) return stopped;
        await _storage.PublishPreservationAsync(command, _paths.DeploymentRoot, cancellationToken).ConfigureAwait(false);
        return stopped with { Output = $"ProcessStopOutcome={stopped.Stage}; PreservationConsistency={(stopped.Stage == "force-stopped" ? "Potentially inconsistent" : "Consistent")}" };
    }

    private async Task<WorkerCommandExecutionResult> FaultStopAsync(
        WorkerCommandClaimResponse command,
        CounterStrike2CommandPayload payload,
        CancellationToken cancellationToken)
    {
        var deadline = payload.Command.FaultEvidenceDeadline
                       ?? throw new InvalidDataException("Fault stop has no evidence deadline.");
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return WorkerCommandExecutionResult.Failure("fault-evidence-window-expired", "fault-evidence-deadline-expired", "The fault evidence deadline expired.");
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(remaining);
        var logs = await CollectLogsAsync(command.Secrets.Values, bounded.Token).ConfigureAwait(false);
        var stopped = await StopAndPreserveAsync(command, payload, bounded.Token).ConfigureAwait(false);
        return stopped with { Output = $"{logs.Output}\n{stopped.Output}" };
    }

    private async Task<WorkerCommandExecutionResult> MaintainAsync(bool validate, CancellationToken cancellationToken)
    {
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.IsRunning)
            return WorkerCommandExecutionResult.Failure("maintenance-running", "game-running", "Stop CS2 before installing or updating it.");
        return await InstallAsync(validate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerCommandExecutionResult> InstallAsync(bool validate, CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        var script = BuildSteamScript(validate);
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            _options.SteamCmd.TemporaryDirectory, "utf-cs2-steamcmd", ".runscript", script, cancellationToken).ConfigureAwait(false);
        var startInfo = _launcher.CreateSteamCmd(
            _options.SteamCmd.SteamCmdPath,
            _paths.InstallPath,
            ["+runscript", temporary.Path]);
        startInfo.Environment["HOME"] = _options.SteamCmd.SteamHomeDirectory ?? _paths.InstallPath;
        var result = await _runner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || !File.Exists(_paths.ExecutablePath))
            return WorkerCommandExecutionResult.Failure("steamcmd-failed", "cs2-install-failed", result.Error ?? "SteamCMD failed.");
        await File.WriteAllTextAsync(_paths.PreparationMarkerPath, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return WorkerCommandExecutionResult.Success(validate ? "validated" : "installed") with
        {
            StorageMeasurements =
            [
                new WorkerStorageMeasurement
                {
                    ComponentKind = "runtime",
                    ApplicationIdentifier = CounterStrike2RuntimePaths.ApplicationId.ToString(CultureInfo.InvariantCulture),
                    ItemIdentifier = "dedicated-server",
                    InstalledBytes = Measure(_paths.InstallPath),
                    VersionMetadata = ReadManifestBuildId(),
                },
            ],
        };
    }

    private async Task RenderConfigurationAsync(
        CounterStrike2CommandPayload payload,
        IReadOnlyDictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        var rendered = GameConfigurationImplementations.Get("cs2").Render(payload.Command.Configuration).Single();
        _paths.EnsureDirectories();
        var text = Encoding.UTF8.GetString(rendered.Content);
        text += $"rcon_password {Quote(secrets["rcon-password"])}\n";
        if (secrets.TryGetValue("join-password", out var joinPassword))
            text += $"sv_password {Quote(joinPassword)}\n";
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            _paths.ConfigurationPath, ".utf-cs2-config", ".tmp", text, cancellationToken).ConfigureAwait(false);
        File.Move(temporary.Path, _paths.ConfigurationFilePath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_paths.ConfigurationFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
    }

    private static IReadOnlyList<string> BuildArguments(
        CounterStrike2CommandPayload payload,
        IReadOnlyDictionary<string, string> secrets)
    {
        var (mapArgument, map) = payload.GetMap();
        var (gameType, gameMode) = payload.GetMode();
        return
        [
            "-dedicated", "-console", "-usercon", "-port", (payload.Command.Port ?? 27015).ToString(CultureInfo.InvariantCulture),
            "+game_type", gameType.ToString(CultureInfo.InvariantCulture),
            "+game_mode", gameMode.ToString(CultureInfo.InvariantCulture),
            mapArgument, map,
            "+exec", "utf-server.cfg",
            "+sv_setsteamaccount", secrets["gslt"],
        ];
    }

    private async Task<WorkerCommandExecutionResult> CollectLogsAsync(IEnumerable<string> secrets, CancellationToken cancellationToken)
    {
        var files = new[] { _paths.LogPath, Path.Combine(_paths.InstallPath, "game", "csgo") }
            .Where(Directory.Exists)
            .SelectMany(q => Directory.EnumerateFiles(q, "*.log", SearchOption.TopDirectoryOnly))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(4)
            .ToArray();
        if (files.Length == 0) return WorkerCommandExecutionResult.Success("logs-empty", "No CS2 logs are available.");
        var output = new StringBuilder();
        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            output.Append("== ").Append(Path.GetFileName(file)).AppendLine(" ==").AppendLine(text.Length > 4096 ? text[^4096..] : text);
        }
        return WorkerCommandExecutionResult.Success("logs-collected", WorkerSecretRedactor.Redact(output.ToString(), secrets));
    }

    private string BuildSteamScript(bool validate)
    {
        var builder = new StringBuilder("@ShutdownOnFailedCommand 1\n@NoPromptForPassword 1\n");
        builder.Append("force_install_dir ").AppendLine(QuoteSteam(_paths.InstallPath));
        builder.Append("login anonymous\napp_update 730");
        if (validate) builder.Append(" validate");
        return builder.Append("\nquit\n").ToString();
    }

    private static void RequireSecrets(WorkerCommandClaimResponse command)
    {
        foreach (var key in new[] { "rcon-password", "gslt" })
            if (!command.Secrets.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"Required CS2 secret '{key}' is unavailable.");
    }

    private bool IsPrepared() => File.Exists(_paths.ExecutablePath) && File.Exists(_paths.PreparationMarkerPath);
    private static TimeSpan StopTimeout(CounterStrike2CommandPayload payload) => TimeSpan.FromSeconds(payload.Command.GracefulStopTimeoutSeconds);
    private async Task RequestGracefulQuitAsync(WorkerCommandClaimResponse command, CounterStrike2CommandPayload payload, CancellationToken cancellationToken)
    {
        if (!command.Secrets.TryGetValue("rcon-password", out var password)) return;
        try
        {
            var sent = await _rcon.RequestQuitAsync(payload.Command.Port ?? 27015, password,
                TimeSpan.FromSeconds(Math.Min(5, payload.Command.GracefulStopTimeoutSeconds)), cancellationToken).ConfigureAwait(false);
            if (!sent) return;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Min(5, payload.Command.GracefulStopTimeoutSeconds));
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
                if (!observation.IsRunning) return;
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
        }
    }
    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", string.Empty).Replace("\n", string.Empty)}\"";
    private static string QuoteSteam(string value) => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private string? ReadManifestBuildId()
    {
        if (!File.Exists(_paths.RuntimeManifestPath)) return null;
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
