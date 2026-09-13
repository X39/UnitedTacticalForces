using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
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

    [Fact]
    public async Task ReconcileRedelivery_Stop_RepublishesPreservationIfProcessStoppedBeforeStopHandlerFinished()
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

        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("already-stopped", result.Stage);
        Assert.Equal(1, processController.StopCount);
        Assert.True(storage.PublishCount > 0);
    }

    [Fact]
    public async Task ReconcileRedelivery_FaultStop_RepublishesPreservationWhenAlreadyStopped()
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

        var payload = JsonNode.Parse(Command(connection, "fault-stop").Payload)!.AsObject();
        payload["faultEvidenceDeadline"] = JsonValue.Create(DateTimeOffset.UtcNow.AddSeconds(10));
        var command = Command(connection, "fault-stop") with
        {
            Payload = payload.ToJsonString(CloudWorkerJson.Options),
        };

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

        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("already-stopped", result.Stage);
        Assert.Equal(1, processController.StopCount);
        Assert.True(storage.PublishCount > 0);
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

    private static WorkerCommandClaimResponse Command(CloudWorkerConnectionConfig connection, string action)
    {
        var payload = new WorkerCommandPayload
        {
            ServerId = connection.ServerId,
            DeploymentId = connection.DeploymentId,
            GameIdentifier = "aska",
            ActionIdentifier = action,
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
        public int StopCount { get; private set; }

        public Task<AskaProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new AskaProcessObservation(false, null, null, null));

        public Task<WorkerCommandExecutionResult> StartAsync(
            IEnumerable<string> secrets,
            CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("not-used"));

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
