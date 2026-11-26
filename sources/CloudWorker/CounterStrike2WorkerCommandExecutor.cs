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
    private readonly IA2sInfoClient _a2s;
    private readonly WorkerContentAcquisitionService _contentAcquisition;

    /// <summary>
    /// The RCON session of the latest successful Start or Restart command. It lives in memory only, is replaced by
    /// the next start and cleared when a stop succeeds.
    /// </summary>
    private volatile HeldRconSession? _heldRconSession;

    /// <summary>The game port used when neither the command nor the persisted identity names one.</summary>
    private const int DefaultGamePort = 27015;

    /// <summary>The time Counter-Strike 2 gets to answer one player query.</summary>
    private static readonly TimeSpan PlayerQueryTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Initializes a Counter-Strike 2 executor and its deployment-bound collaborators.</summary>
    /// <param name="connection">Deployment-scoped worker connection.</param>
    /// <param name="options">Worker runtime options.</param>
    /// <param name="runner">Optional command runner for tests.</param>
    /// <param name="process">Optional process controller for tests.</param>
    /// <param name="storage">Optional storage transfer for tests.</param>
    /// <param name="rcon">Optional RCON client for tests; defaults to the loopback client.</param>
    /// <param name="a2s">Optional A2S_INFO client for tests; defaults to the loopback client.</param>
    public CounterStrike2WorkerCommandExecutor(
        CloudWorkerConnectionConfig connection,
        CloudWorkerOptions options,
        IProcessCommandRunner? runner = null,
        ICounterStrike2ProcessController? process = null,
        ICloudWorkerStorageTransfer? storage = null,
        ICounterStrike2RconClient? rcon = null,
        IA2sInfoClient? a2s = null)
    {
        _a2s = a2s ?? new A2sInfoClient();
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
                "terminate" => await TerminateAndForgetAsync(cancellationToken).ConfigureAwait(false),
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
            _ = await TerminateAndForgetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Terminates the game process and forgets the held RCON session when the process is gone.</summary>
    /// <param name="cancellationToken">Cancels the termination.</param>
    /// <returns>The outcome of the termination.</returns>
    private async Task<WorkerCommandExecutionResult> TerminateAndForgetAsync(CancellationToken cancellationToken)
    {
        var result = await _process.TerminateAsync(cancellationToken).ConfigureAwait(false);
        if (result.Succeeded) _heldRconSession = null;
        return result;
    }

    /// <summary>Keeps the RCON password of a Start or Restart command in memory for player observation.</summary>
    /// <param name="command">The claimed command whose secrets carry the RCON password.</param>
    /// <param name="payload">The parsed payload that names the game port.</param>
    private void HoldRconSession(WorkerCommandClaimResponse command, CounterStrike2CommandPayload payload)
    {
        if (command.Secrets.TryGetValue("rcon-password", out var password) && !string.IsNullOrWhiteSpace(password))
            _heldRconSession = new HeldRconSession(payload.Command.Port ?? 27015, password);
    }

    /// <summary>The loopback RCON endpoint and password of the running server, kept in memory only.</summary>
    /// <param name="port">The game and RCON port the server was started on.</param>
    /// <param name="password">The RCON password; the type deliberately has no <see cref="object.ToString" /> output of it.</param>
    private sealed class HeldRconSession(int port, string password)
    {
        /// <summary>Gets the game and RCON port.</summary>
        public int Port { get; } = port;

        /// <summary>Gets the RCON password.</summary>
        public string Password { get; } = password;

        /// <inheritdoc />
        public override string ToString() => nameof(HeldRconSession);
    }

    /// <summary>Reports the human players of the running Counter-Strike 2 server.</summary>
    /// <param name="cancellationToken">Cancels the observation.</param>
    /// <returns>
    /// The human count and capacity, measuring when no process runs, or query-failed when neither the RCON status
    /// request nor the A2S_INFO fallback can be answered.
    /// </returns>
    /// <remarks>
    /// Per D-06 the existing RCON client is the primary source: the <c>status</c> response is parsed for its human
    /// players using the RCON password that this executor holds in memory only. Neither the password nor a reply
    /// body is logged, persisted or put into an exception message. After a worker restart the in-memory password
    /// is gone, so Counter-Strike 2 reports players through A2S_INFO on the persisted game port
    /// until the next Start (or Restart) hands the executor a password again; the same fallback covers a status
    /// reply without a parseable players line. This keeps the secret-handling rules intact. When neither source
    /// answers the observation is query-failed, never a guessed count.
    /// </remarks>
    public async Task<WorkerPlayerObservation> ObservePlayersAsync(CancellationToken cancellationToken)
    {
        using var observation = await _process.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (!observation.IsRunning)
            return WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.Measuring);
        var session = _heldRconSession;
        if (session is not null)
        {
            string? status;
            try
            {
                status = await _rcon.RequestStatusAsync(session.Port, session.Password, PlayerQueryTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SocketException or IOException or InvalidDataException
                                                  or EndOfStreamException
                                                  || exception is OperationCanceledException
                                                  && !cancellationToken.IsCancellationRequested)
            {
                status = null;
            }
            if (CounterStrike2StatusParser.TryParse(status, out var humans, out var capacity))
                return WorkerPlayerObservation.Players(humans, capacity);
        }
        var info = await _a2s.QueryAsync(observation.Identity?.GamePort ?? DefaultGamePort, PlayerQueryTimeout, cancellationToken)
            .ConfigureAwait(false);
        return info is null
            ? WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.QueryFailed)
            : WorkerPlayerObservation.Players(info.Humans, info.MaxPlayers);
    }

    /// <inheritdoc />
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
        {
            HoldRconSession(command, payload);
            return WorkerCommandExecutionResult.Success("already-running", processId: current.Identity.ProcessId, processStartedAt: current.Identity.ProcessStartedAt);
        }
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
        if (started.Succeeded) HoldRconSession(command, payload);
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
        _heldRconSession = null;
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
        _heldRconSession = null;
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
