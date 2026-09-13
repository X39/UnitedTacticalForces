using System.Diagnostics;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public sealed class AskaImplementationTests
{
    [Fact]
    public void ConfigurationRenderer_RejectsPropertyInjection()
    {
        var implementation = new AskaConfigurationImplementation();
        var serverName = implementation.Definitions.Single(q => q.Setting == "server name");

        Assert.Throws<InvalidDataException>(() => implementation.Render(new Dictionary<string, JsonElement>
        {
            [serverName.Key] = JsonSerializer.SerializeToElement("UTF\nauthentication token = stolen"),
        }));
    }

    [Fact]
    public async Task RuntimeProperties_KeepSecretsOutOfPreservedSaveIdentity()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AskaRuntimePaths(directory.Path, Path.Combine(directory.Path, "agent"), Guid.NewGuid(),
            "AskaServer.exe");
        var renderer = new AskaConfigurationRenderer();
        var values = DefaultConfiguration();
        const string token = "private-auth-token";
        const string password = "private-password";

        await renderer.RenderAsync(paths, values, new Dictionary<string, string>
        {
            ["authentication-token"] = token,
            ["join-password"] = password,
        }, CancellationToken.None);
        var properties = await File.ReadAllTextAsync(paths.PropertiesPath);
        Assert.Contains($"authentication token = {token}", properties, StringComparison.Ordinal);
        Assert.Contains($"password = {password}", properties, StringComparison.Ordinal);
        Assert.Contains("steam game port = 27015", properties, StringComparison.Ordinal);
        Assert.Contains("steam query port = 27016", properties, StringComparison.Ordinal);

        await File.WriteAllTextAsync(paths.PropertiesPath,
            properties.Replace("save id = \n", "save id = generated_world_42\n", StringComparison.Ordinal));
        await renderer.SynchronizeSaveIdAsync(paths, CancellationToken.None);

        Assert.Equal("generated_world_42\n", await File.ReadAllTextAsync(paths.SaveIdPath));
        Assert.DoesNotContain(token, await File.ReadAllTextAsync(paths.SaveIdPath), StringComparison.Ordinal);
        Assert.DoesNotContain(password, await File.ReadAllTextAsync(paths.SaveIdPath), StringComparison.Ordinal);
        renderer.DeleteRuntimeConfiguration(paths);
        Assert.False(File.Exists(paths.PropertiesPath));

        var restoredValues = values.ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal);
        var serverName = new AskaConfigurationImplementation().Definitions.Single(q => q.Setting == "server name");
        restoredValues[serverName.Key] = JsonSerializer.SerializeToElement("Requested server name");
        await renderer.RenderAsync(paths, restoredValues, new Dictionary<string, string>
        {
            ["authentication-token"] = token,
        }, CancellationToken.None);
        var restoredProperties = await File.ReadAllTextAsync(paths.PropertiesPath);
        Assert.Contains("save id = generated_world_42", restoredProperties, StringComparison.Ordinal);
        Assert.Contains("server name = Requested server name", restoredProperties, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeProperties_RequireAuthenticationToken()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AskaRuntimePaths(
            directory.Path, Path.Combine(directory.Path, "agent"), Guid.NewGuid(), "AskaServer.exe");

        await Assert.ThrowsAsync<InvalidDataException>(() => new AskaConfigurationRenderer().RenderAsync(
            paths,
            DefaultConfiguration(),
            new Dictionary<string, string>(),
            CancellationToken.None));
        Assert.False(File.Exists(paths.PropertiesPath));
    }

    [Fact]
    public void Payload_RejectsCrossDeploymentAttribution()
    {
        var connection = Connection();
        var envelope = Command(connection, "start");
        var payload = AskaCommandPayload.Parse(envelope.Payload);

        Assert.Throws<InvalidDataException>(() => payload.Validate(
            connection with { DeploymentId = Guid.NewGuid() }, envelope));
    }

    /// <summary>Verifies that only fault-stop commands carry their mandatory evidence deadline.</summary>
    [Fact]
    public void Payload_RequiresFaultEvidenceDeadlineExactlyForFaultStop()
    {
        var connection = Connection();
        var missingDeadline = Command(connection, "fault-stop");
        var unexpectedDeadline = Command(
            connection,
            "stop",
            faultEvidenceDeadline: DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<InvalidDataException>(() =>
            AskaCommandPayload.Parse(missingDeadline.Payload).Validate(connection, missingDeadline));
        Assert.Throws<InvalidDataException>(() =>
            AskaCommandPayload.Parse(unexpectedDeadline.Payload).Validate(connection, unexpectedDeadline));
    }

    [Fact]
    public async Task Install_ForcesWindowsSteamPlatformAndInitializesWineBeforePreparedMarker()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var steamCmd = Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmd, "fixture");
        var options = Options(directory.Path, steamCmd);
        var paths = new AskaRuntimePaths(
            options.RuntimeRoot, options.StateDirectory, connection.DeploymentId, options.Aska.ExecutableRelativePath);
        var runner = new PreparingRunner(paths);
        var executor = new AskaWorkerCommandExecutor(
            connection, options, runner, new StoppedProcessController(), new NoOpStorageTransfer());

        var result = await executor.ExecuteAsync(Command(connection, "install"), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(2, runner.RunCount);
        Assert.Contains("@sSteamCmdForcePlatformType windows", runner.SteamScript, StringComparison.Ordinal);
        Assert.Contains("login anonymous", runner.SteamScript, StringComparison.Ordinal);
        Assert.Contains("app_update 3246670", runner.SteamScript, StringComparison.Ordinal);
        Assert.True(File.Exists(paths.PreparationMarkerPath));
        Assert.True(runner.WinePrefixExistedAfterInitialization);
    }

    /// <summary>
    /// Verifies that ASKA SteamCMD runscripts are transferred to the configured Steam identity without granting
    /// group or other access.
    /// </summary>
    /// <returns>A task that completes after the install command is exercised.</returns>
    [Fact]
    public async Task Install_OnLinux_TransfersRunScriptToSteamIdentityWithOwnerReadOnlyAccess()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var steamCmd = Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmd, "fixture");
        var effectiveUserId = int.Parse(
            (await File.ReadAllLinesAsync("/proc/self/status"))
            .Single(q => q.StartsWith("Uid:", StringComparison.Ordinal))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1]);
        var options = Options(directory.Path, steamCmd, effectiveUserId);
        var paths = new AskaRuntimePaths(
            options.RuntimeRoot, options.StateDirectory, connection.DeploymentId, options.Aska.ExecutableRelativePath);
        var runner = new PreparingRunner(paths);
        var executor = new AskaWorkerCommandExecutor(
            connection, options, runner, new StoppedProcessController(), new NoOpStorageTransfer());

        var result = await executor.ExecuteAsync(Command(connection, "install"), CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(runner.SteamScriptWasOwnerReadOnly);
    }

    [Fact]
    public async Task Install_WhenWineInitializationFails_DoesNotMarkRuntimePrepared()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var steamCmd = Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmd, "fixture");
        var options = Options(directory.Path, steamCmd);
        var paths = new AskaRuntimePaths(
            options.RuntimeRoot, options.StateDirectory, connection.DeploymentId, options.Aska.ExecutableRelativePath);
        var runner = new PreparingRunner(paths, failWine: true);
        var executor = new AskaWorkerCommandExecutor(
            connection, options, runner, new StoppedProcessController(), new NoOpStorageTransfer());

        var result = await executor.ExecuteAsync(Command(connection, "install"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("aska-wine-prefix-failed", result.ErrorCode);
        Assert.False(File.Exists(paths.PreparationMarkerPath));
    }

    /// <summary>
    /// Verifies that a redelivered stop reexecutes its handler when preservation may have been interrupted.
    /// </summary>
    /// <returns>A task that completes after reconciliation and replay.</returns>
    [Fact]
    public async Task ReconcileRedelivery_Stop_ReexecutesHandlerIfPreservationMayNotHaveFinished()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var paths = new AskaRuntimePaths(
            Path.Combine(directory.Path, "runtime"),
            Path.Combine(directory.Path, "agent"),
            connection.DeploymentId,
            "AskaServer.exe");
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var executor = new AskaWorkerCommandExecutor(
            connection,
            Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh")),
            process: processController,
            storage: storage);

        var result = await executor.ReconcileRedeliveryAsync(
            Command(connection, "stop"),
            new WorkerCommandJournalEntry(
                Guid.NewGuid(),
                connection.DeploymentId,
                connection.WorkerId,
                1,
                "hash",
                "payload-hash",
                EWorkerCommandJournalState.Running,
                "running",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1234,
                DateTimeOffset.UtcNow,
                null),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);

        var replayed = await executor.ExecuteAsync(Command(connection, "stop"), CancellationToken.None);

        Assert.True(replayed.Succeeded, replayed.ErrorMessage);
        Assert.Equal("already-stopped", replayed.Stage);
        Assert.Equal(1, processController.StopCount);
        Assert.Equal(1, storage.PublishCount);
    }

    /// <summary>
    /// Verifies that a redelivered non-preserving stop removes an ephemeral properties file after a crash.
    /// </summary>
    /// <returns>A task that completes after reconciliation removes the runtime configuration.</returns>
    [Fact]
    public async Task ReconcileRedelivery_StopWithoutPreservation_RemovesRuntimeConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var options = Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh"));
        var paths = new AskaRuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.Aska.ExecutableRelativePath);
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var renderer = new AskaConfigurationRenderer();
        await renderer.RenderAsync(
            paths,
            DefaultConfiguration(),
            new Dictionary<string, string>
            {
                ["authentication-token"] = "crash-test-token",
                ["join-password"] = "crash-test-password",
            },
            CancellationToken.None);
        Assert.True(File.Exists(paths.PropertiesPath));

        var executor = new AskaWorkerCommandExecutor(
            connection,
            options,
            process: processController,
            storage: storage,
            configurationRenderer: renderer);

        var result = await executor.ReconcileRedeliveryAsync(
            Command(connection, "stop", preserveOnStop: false),
            JournalEntry(connection),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("reconciled-stopped", result.Stage);
        Assert.False(File.Exists(paths.PropertiesPath));
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);
    }

    /// <summary>
    /// Verifies that a redelivered termination removes an ephemeral properties file after a crash.
    /// </summary>
    /// <returns>A task that completes after reconciliation removes the runtime configuration.</returns>
    [Fact]
    public async Task ReconcileRedelivery_Terminate_RemovesRuntimeConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var options = Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh"));
        var paths = new AskaRuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.Aska.ExecutableRelativePath);
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var renderer = new AskaConfigurationRenderer();
        await renderer.RenderAsync(
            paths,
            DefaultConfiguration(),
            new Dictionary<string, string>
            {
                ["authentication-token"] = "crash-test-token",
                ["join-password"] = "crash-test-password",
            },
            CancellationToken.None);
        Assert.True(File.Exists(paths.PropertiesPath));

        var executor = new AskaWorkerCommandExecutor(
            connection,
            options,
            process: processController,
            storage: storage,
            configurationRenderer: renderer);

        var result = await executor.ReconcileRedeliveryAsync(
            Command(connection, "terminate"),
            JournalEntry(connection),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("reconciled-stopped", result.Stage);
        Assert.False(File.Exists(paths.PropertiesPath));
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);
    }

    /// <summary>
    /// Verifies that a redelivered fault-stop reexecutes diagnostics, stop, and preservation as one bounded handler.
    /// </summary>
    /// <returns>A task that completes after reconciliation and replay.</returns>
    [Fact]
    public async Task ReconcileRedelivery_FaultStop_ReexecutesCompleteBoundedHandlerWhenAlreadyStopped()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var paths = new AskaRuntimePaths(
            Path.Combine(directory.Path, "runtime"),
            Path.Combine(directory.Path, "agent"),
            connection.DeploymentId,
            "AskaServer.exe");
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var executor = new AskaWorkerCommandExecutor(
            connection,
            Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh")),
            process: processController,
            storage: storage);

        var command = Command(
            connection,
            "fault-stop",
            faultEvidenceDeadline: DateTimeOffset.UtcNow.AddSeconds(10));

        var result = await executor.ReconcileRedeliveryAsync(
            command,
            new WorkerCommandJournalEntry(
                Guid.NewGuid(),
                connection.DeploymentId,
                connection.WorkerId,
                1,
                "hash",
                "payload-hash",
                EWorkerCommandJournalState.Running,
                "running",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1234,
                DateTimeOffset.UtcNow,
                null),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);

        var replayed = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(replayed.Succeeded, replayed.ErrorMessage);
        Assert.Equal("already-stopped", replayed.Stage);
        Assert.Equal(1, processController.StopCount);
        Assert.Equal(1, storage.PublishCount);
    }

    /// <summary>
    /// Verifies that an expired fault-evidence deadline takes precedence over process observation during recovery.
    /// </summary>
    /// <returns>A task that completes after reconciliation.</returns>
    [Fact]
    public async Task ReconcileRedelivery_FaultStopExpired_DoesNotObserveOrReplay()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var executor = new AskaWorkerCommandExecutor(
            connection,
            Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh")),
            process: processController,
            storage: storage);
        var command = Command(connection, "fault-stop", faultEvidenceDeadline: DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await executor.ReconcileRedeliveryAsync(
            command,
            JournalEntry(connection),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.Equal("fault-evidence-deadline-expired", result.ErrorCode);
        Assert.Equal(0, processController.ObserveCount);
        Assert.Equal(0, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);
    }

    /// <summary>Verifies that ASKA obeys the shared preservation ownership flag during an ordinary stop.</summary>
    /// <returns>A task that completes after stop execution.</returns>
    [Fact]
    public async Task Execute_StopWithoutPreservationOwnership_DoesNotPublishSnapshot()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var executor = new AskaWorkerCommandExecutor(
            connection,
            Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh")),
            process: processController,
            storage: storage);

        var result = await executor.ExecuteAsync(
            Command(connection, "stop", preserveOnStop: false),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1, processController.StopCount);
        Assert.Equal(0, storage.PublishCount);
    }

    /// <summary>Verifies that fault-stop reports deadline expiry when bounded stop work runs out of time.</summary>
    /// <returns>A task that completes after the evidence deadline expires.</returns>
    [Fact]
    public async Task Execute_FaultStopDeadlineExpiresDuringStop_ReturnsDeadlineFailure()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var processController = new AskaReplayProcessController
        {
            StopDelay = TimeSpan.FromSeconds(5),
        };
        var executor = new AskaWorkerCommandExecutor(
            connection,
            Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh")),
            process: processController,
            storage: new AskaReplayStorageTransfer());
        var command = Command(
            connection,
            "fault-stop",
            faultEvidenceDeadline: DateTimeOffset.UtcNow.AddMilliseconds(100));

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("fault-evidence-deadline-expired", result.ErrorCode);
    }

    /// <summary>
    /// Verifies that an unreadable runtime log remains best-effort and cannot prevent fault stop preservation.
    /// </summary>
    /// <returns>A task that completes after fault stop execution.</returns>
    [Fact]
    public async Task Execute_FaultStopLogReadFails_StopsAndPublishesPreservation()
    {
        using var directory = new TemporaryDirectory();
        var connection = Connection();
        var options = Options(directory.Path, Path.Combine(directory.Path, "steamcmd.sh"));
        var paths = new AskaRuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            connection.DeploymentId,
            options.Aska.ExecutableRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RuntimeLogPath)!);
        await File.WriteAllTextAsync(paths.RuntimeLogPath, "runtime log");
        var processController = new AskaReplayProcessController();
        var storage = new AskaReplayStorageTransfer();
        var executor = new AskaWorkerCommandExecutor(
            connection,
            options,
            process: processController,
            storage: storage)
        {
            LogReader = (_, _) => throw new IOException("Cannot read test-token from the runtime log."),
        };
        var command = Command(
            connection,
            "fault-stop",
            faultEvidenceDeadline: DateTimeOffset.UtcNow.AddSeconds(10));

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1, processController.StopCount);
        Assert.Equal(1, storage.PublishCount);
        Assert.Contains("Fault diagnostics unavailable", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("test-token", result.Output, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, JsonElement> DefaultConfiguration()
        => new AskaConfigurationImplementation().Definitions.ToDictionary(
            q => q.Key,
            q => q.DefaultValue!.Value,
            StringComparer.Ordinal);

    private static CloudWorkerConnectionConfig Connection()
        => new()
        {
            ApiBaseUrl = "https://control.example/api/",
            WorkerId = Guid.NewGuid(),
            DeploymentId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            GameIdentifier = "aska",
        };

    private static WorkerCommandClaimResponse Command(
        CloudWorkerConnectionConfig connection,
        string action,
        bool preserveOnStop = true,
        DateTimeOffset? faultEvidenceDeadline = null)
    {
        var payload = new WorkerCommandPayload
        {
            ServerId = connection.ServerId,
            DeploymentId = connection.DeploymentId,
            GameIdentifier = "aska",
            ActionIdentifier = action,
            PreserveOnStop = preserveOnStop,
            FaultEvidenceDeadline = faultEvidenceDeadline,
            GracefulStopAttempts = 2,
            GracefulStopTimeoutSeconds = 30,
            Port = 27015,
            Configuration = DefaultConfiguration(),
        };
        return new WorkerCommandClaimResponse
        {
            CommandId = Guid.NewGuid(),
            LifecycleJobId = Guid.NewGuid(),
            DeploymentId = connection.DeploymentId,
            WorkerId = connection.WorkerId,
            Sequence = 1,
            ActionIdentifier = action,
            Payload = JsonSerializer.Serialize(payload, CloudWorkerJson.Options),
            Secrets = new Dictionary<string, string>
            {
                ["authentication-token"] = "test-token",
            },
            Attempt = 1,
        };
    }

    /// <summary>Creates a running journal entry for redelivery tests.</summary>
    /// <param name="connection">Deployment-scoped worker connection.</param>
    /// <returns>A running command journal entry.</returns>
    private static WorkerCommandJournalEntry JournalEntry(CloudWorkerConnectionConfig connection)
        => new(
            Guid.NewGuid(),
            connection.DeploymentId,
            connection.WorkerId,
            1,
            "hash",
            "payload-hash",
            EWorkerCommandJournalState.Running,
            "running",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1234,
            DateTimeOffset.UtcNow,
            null);

    private static CloudWorkerOptions Options(string root, string steamCmd, int? steamUserId = null)
        => new(
            Path.Combine(root, "bootstrap.json"),
            Path.Combine(root, "agent"),
            Path.Combine(root, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            true,
            false,
            new Arma3WorkerOptions(
                steamCmd,
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30),
                SteamUserId: steamUserId)
            {
                TemporaryDirectory = Path.Combine(root, "tmp"),
            })
        {
            Aska = new AskaWorkerOptions(
                "AskaServer.exe", "/usr/bin/wine", "/usr/bin/wineboot", "/usr/bin/xvfb-run", "/usr/bin/setsid",
                TimeSpan.FromSeconds(30)),
        };

    private sealed class PreparingRunner(AskaRuntimePaths paths, bool failWine = false) : IProcessCommandRunner
    {
        public int RunCount { get; private set; }
        public string SteamScript { get; private set; } = string.Empty;
        /// <summary>Gets whether the SteamCMD runscript had owner-read-only permissions when execution began.</summary>
        public bool SteamScriptWasOwnerReadOnly { get; private set; }
        public bool WinePrefixExistedAfterInitialization { get; private set; }

        public async Task<ProcessCommandResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            RunCount++;
            if (RunCount == 1)
            {
                if (!OperatingSystem.IsWindows())
                    SteamScriptWasOwnerReadOnly = File.GetUnixFileMode(startInfo.ArgumentList[^1])
                                                  == UnixFileMode.UserRead;
                SteamScript = await File.ReadAllTextAsync(startInfo.ArgumentList[^1], cancellationToken);
                Directory.CreateDirectory(paths.InstallPath);
                await File.WriteAllTextAsync(paths.ExecutablePath, "fixture", cancellationToken);
                return new ProcessCommandResult(true, 0, string.Empty, string.Empty);
            }
            if (failWine)
                return new ProcessCommandResult(false, 1, string.Empty, "wine failed");
            Directory.CreateDirectory(paths.WinePrefixPath);
            WinePrefixExistedAfterInitialization = true;
            return new ProcessCommandResult(true, 0, string.Empty, string.Empty);
        }
    }

    private sealed class StoppedProcessController : IAskaProcessController
    {
        public int StopCount { get; private set; }
        public Task<AskaProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new AskaProcessObservation(false, null, null, null));
        public Task<WorkerCommandExecutionResult> StartAsync(
            IEnumerable<string> secrets,
            CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("started"));
        public Task<WorkerCommandExecutionResult> StopAsync(
            bool force,
            TimeSpan timeout,
            int attempts,
            CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.FromResult(WorkerCommandExecutionResult.Success("already-stopped"));
        }
        public Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("already-stopped"));
    }

    private sealed class AskaReplayProcessController : IAskaProcessController
    {
        /// <summary>Gets the number of process observations.</summary>
        public int ObserveCount { get; private set; }

        /// <summary>Gets the number of stop attempts.</summary>
        public int StopCount { get; private set; }

        /// <summary>Gets the optional delay applied to stop attempts.</summary>
        public TimeSpan StopDelay { get; init; }

        public Task<AskaProcessObservation> ObserveAsync(CancellationToken cancellationToken)
        {
            ObserveCount++;
            return Task.FromResult(new AskaProcessObservation(false, null, null, null));
        }

        public Task<WorkerCommandExecutionResult> StartAsync(
            IEnumerable<string> secrets,
            CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("not-used"));

        public async Task<WorkerCommandExecutionResult> StopAsync(
            bool force,
            TimeSpan timeout,
            int attempts,
            CancellationToken cancellationToken)
        {
            StopCount++;
            if (StopDelay > TimeSpan.Zero)
                await Task.Delay(StopDelay, cancellationToken);
            return WorkerCommandExecutionResult.Success("already-stopped");
        }

        public Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("not-used"));
    }

    private sealed class AskaReplayStorageTransfer : ICloudWorkerStorageTransfer
    {
        public int PublishCount { get; private set; }

        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishPreservationAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
        {
            PublishCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpStorageTransfer : ICloudWorkerStorageTransfer
    {
        public Task ResetPersistentOverlayAsync(string deploymentRoot, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task PublishPreservationAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
