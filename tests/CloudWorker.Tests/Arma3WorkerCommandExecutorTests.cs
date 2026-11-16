using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class Arma3WorkerCommandExecutorTests
{
    /// <summary>Checks measured capacity immediately before SteamCMD, independently of control-plane approval.</summary>
    /// <param name="availableBytes">The actual free bytes observed after formatting and mounting.</param>
    /// <param name="expectedFailure">The expected rejection code, or null for sufficient capacity.</param>
    /// <returns>The asynchronous verification.</returns>
    [Theory]
    [InlineData(5_999L, "storage-capacity-insufficient")]
    [InlineData(6_000L, null)]
    [InlineData(6_001L, null)]
    [InlineData(null, "storage-capacity-unavailable")]
    public async Task Start_MountedUsableCapacityHonorsPeakBoundary(
        long? availableBytes,
        string? expectedFailure)
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "bootstrap.json"),
            System.IO.Path.Combine(directory.Path, "state"),
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(steamCmdPath, "anonymous", null, null, "arma3server_x64", TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = System.IO.Path.Combine(directory.Path, "tmp"),
            });
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot, options.StateDirectory, bootstrap.DeploymentId, options.Arma3.ExecutableName);
        var runner = new PreparingCommandRunner(paths.ExecutablePath);
        var process = new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: runner,
            processController: process,
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath))
        {
            StorageObservationOverride = () => availableBytes.HasValue
                ? (10_000L, availableBytes, 2_000L)
                : (null, null, null),
        };
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId, bootstrap.DeploymentId, bootstrap.ServerId);
        var payload = JsonNode.Parse(command.Payload)!.AsObject();
        payload["peakStorageRequiredBytes"] = 8_000L;
        command = command with { Payload = payload.ToJsonString() };

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(expectedFailure is null, result.Succeeded);
        Assert.Equal(expectedFailure, result.ErrorCode);
        Assert.Equal(expectedFailure is null ? 1 : 0, runner.RunCount);
        Assert.Equal(expectedFailure is null ? 1 : 0, process.StartCount);
    }

    [Fact]
    public async Task Start_OnFreshRuntime_PreparesWithSteamCmdBeforeLaunching()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        var executableName = "arma3server_x64";
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "bootstrap.json"),
            System.IO.Path.Combine(directory.Path, "state"),
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                steamCmdPath,
                "anonymous",
                null,
                null,
                executableName,
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = System.IO.Path.Combine(directory.Path, "tmp"),
            });
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            executableName);
        var staleTemporaryDirectory = System.IO.Path.Combine(
            options.Arma3.TemporaryDirectory,
            $"utf-steamcmd-{bootstrap.DeploymentId:N}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staleTemporaryDirectory);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                staleTemporaryDirectory,
                $"utf-steamcmd-{bootstrap.DeploymentId:N}-{Guid.NewGuid():N}.runscript"),
            "stale secret material");
        Directory.CreateDirectory(paths.DeploymentRoot);
        var legacyRunScript = System.IO.Path.Combine(
            paths.DeploymentRoot,
            $"steamcmd-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(legacyRunScript, "legacy stale secret material");
        var commandRunner = new PreparingCommandRunner(paths.ExecutablePath);
        var processController = new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath);
        var storageTransfer = new RecordingStorageTransfer(paths.ServerConfigurationPath);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: commandRunner,
            processController: processController,
            storageTransfer: storageTransfer);
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId,
            "start");

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, commandRunner.RunCount);
        Assert.Equal(1, processController.StartCount);
        Assert.True(processController.ExecutableExistedAtStart);
        Assert.True(storageTransfer.PersistentOverlayResetBeforeConfigurationRender);
        Assert.True(storageTransfer.PersistentSnapshotAppliedAfterConfigurationRender);
        Assert.Equal("persistent-server-config", processController.ServerConfigurationAtStart);
        Assert.True(File.Exists(paths.PreparationMarkerPath));
        Assert.False(Directory.Exists(staleTemporaryDirectory));
        Assert.False(File.Exists(legacyRunScript));
        Assert.True(commandRunner.RunScriptExistedDuringExecution);
        Assert.Equal(
            System.IO.Path.Combine(options.RuntimeRoot, ".utf-tooling", "steam-home"),
            commandRunner.HomeDirectory);
        Assert.DoesNotContain("workshop_download_item", commandRunner.RunScriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain(
            processController.ArgumentsAtStart,
            argument => argument.StartsWith("-mod=", StringComparison.Ordinal));
        Assert.StartsWith(
            System.IO.Path.GetFullPath(options.Arma3.TemporaryDirectory),
            System.IO.Path.GetFullPath(commandRunner.RunScriptPath!),
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            System.IO.Path.GetFullPath(commandRunner.RunScriptPath!)
                .StartsWith(System.IO.Path.GetFullPath(paths.DeploymentRoot), StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(commandRunner.RunScriptPath));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                commandRunner.RunScriptMode);
        }
    }

    [Fact]
    public async Task Start_WithWorkshopContent_DownloadsAllowlistedItemsAndBuildsSingleModArgument()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        var options = CreateOptions(directory.Path, steamCmdPath);
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            options.Arma3.ExecutableName);
        var workshopItemIds = new[] { 450814997UL, 463939057UL };
        var workshopPaths = workshopItemIds.Select(paths.GetSteamWorkshopItemPath).ToArray();
        var commandRunner = new PreparingCommandRunner(
            paths.ExecutablePath,
            preparedDirectories: workshopPaths);
        var processController = new RecordingProcessController(
            paths.ExecutablePath,
            paths.ServerConfigurationPath);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: commandRunner,
            processController: processController,
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath));
        var command = WithContent(
            FlatFileWorkerCommandJournalTests.CreateCommand(
                bootstrap.WorkerId,
                bootstrap.DeploymentId,
                bootstrap.ServerId,
                "start"),
            [
                new WorkerContentReference
                {
                    Source = Arma3CommandPayload.SteamWorkshopContentSource,
                    Identifier = workshopItemIds[0].ToString(),
                    DisplayName = "CBA A3",
                },
                new WorkerContentReference
                {
                    Source = Arma3CommandPayload.SteamWorkshopContentSource,
                    Identifier = workshopItemIds[1].ToString(),
                    DisplayName = "ACE",
                },
            ]);

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains(
            $"force_install_dir \"{paths.InstallPath.Replace("\\", "\\\\", StringComparison.Ordinal)}\"",
            commandRunner.RunScriptContent,
            StringComparison.Ordinal);
        Assert.Contains("workshop_download_item 107410 450814997", commandRunner.RunScriptContent);
        Assert.Contains("workshop_download_item 107410 463939057", commandRunner.RunScriptContent);
        Assert.DoesNotContain("CBA A3", commandRunner.RunScriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain("ACE", commandRunner.RunScriptContent, StringComparison.Ordinal);
        var modArgument = Assert.Single(
            processController.ArgumentsAtStart,
            q => q.StartsWith("-mod=", StringComparison.Ordinal));
        Assert.Equal($"-mod={string.Join(';', workshopPaths)}", modArgument);
    }

    [Fact]
    public async Task Install_WhenSteamCmdOmitsSelectedWorkshopContent_FailsWithoutPreparationMarker()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        var options = CreateOptions(directory.Path, steamCmdPath);
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            options.Arma3.ExecutableName);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: new PreparingCommandRunner(paths.ExecutablePath),
            processController: new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath),
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath));
        var command = WithContent(
            FlatFileWorkerCommandJournalTests.CreateCommand(
                bootstrap.WorkerId,
                bootstrap.DeploymentId,
                bootstrap.ServerId,
                "install"),
            [
                new WorkerContentReference
                {
                    Source = Arma3CommandPayload.SteamWorkshopContentSource,
                    Identifier = "450814997",
                    DisplayName = "CBA A3",
                },
            ]);

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("workshop-content-missing", result.ErrorCode);
        Assert.False(File.Exists(paths.PreparationMarkerPath));
        Assert.DoesNotContain("steam", result.Output ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Install_WhenSteamCmdFails_ReturnsBoundedRedactedProcessAndSteamLogDiagnostics()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd", "steamcmd.sh");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(steamCmdPath)!);
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        var options = CreateOptions(directory.Path, steamCmdPath);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: new FailingCommandRunner(),
            processController: new RecordingProcessController(
                System.IO.Path.Combine(directory.Path, "missing-arma"),
                System.IO.Path.Combine(directory.Path, "missing-server.cfg")),
            storageTransfer: new RecordingStorageTransfer(System.IO.Path.Combine(directory.Path, "server.cfg")));

        var result = await executor.ExecuteAsync(
            FlatFileWorkerCommandJournalTests.CreateCommand(
                bootstrap.WorkerId,
                bootstrap.DeploymentId,
                bootstrap.ServerId,
                "install"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("steamcmd-failed", result.ErrorCode);
        Assert.Equal("native stderr detail", result.ErrorMessage);
        Assert.NotNull(result.Output);
        Assert.True(result.Output.Length <= 16_384);
        Assert.StartsWith("SteamCMD exit code: 42", result.Output, StringComparison.Ordinal);
        Assert.Contains("stdout tail marker", result.Output, StringComparison.Ordinal);
        Assert.Contains("native stderr detail", result.Output, StringComparison.Ordinal);
        Assert.Contains("Steam console diagnostic", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic-secret", result.Output, StringComparison.Ordinal);
        Assert.Contains("[redacted sensitive worker output]", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_WithWorkshopContent_ValidatesServerAndSelectedItems()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd.sh");
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        var options = CreateOptions(directory.Path, steamCmdPath);
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            options.Arma3.ExecutableName);
        const ulong workshopItemId = 450814997;
        var commandRunner = new PreparingCommandRunner(
            paths.ExecutablePath,
            preparedDirectories: [paths.GetSteamWorkshopItemPath(workshopItemId)]);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: commandRunner,
            processController: new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath),
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath));
        var command = WithContent(
            FlatFileWorkerCommandJournalTests.CreateCommand(
                bootstrap.WorkerId,
                bootstrap.DeploymentId,
                bootstrap.ServerId,
                "update"),
            [
                new WorkerContentReference
                {
                    Source = Arma3CommandPayload.SteamWorkshopContentSource,
                    Identifier = workshopItemId.ToString(),
                    DisplayName = "CBA A3",
                },
            ]);

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("app_update 233780 validate", commandRunner.RunScriptContent);
        Assert.Contains("workshop_download_item 107410 450814997 validate", commandRunner.RunScriptContent);
    }

    [Fact]
    public async Task ObserveRuntime_ReportsCoherentDeploymentVolumeTelemetry()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var options = CreateOptions(directory.Path, System.IO.Path.Combine(directory.Path, "steamcmd.sh"));
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            options.Arma3.ExecutableName);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            processController: new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath));

        var status = await executor.ObserveRuntimeAsync(CancellationToken.None);

        Assert.NotNull(status);
        Assert.True(status.DeploymentStorageTotalBytes > 0);
        Assert.True(status.DeploymentStorageAvailableBytes >= 0);
        Assert.True(status.DeploymentStorageAvailableBytes <= status.DeploymentStorageTotalBytes);
    }

    [Fact]
    public async Task Install_RedactsExactSteamSecretWithoutHidingInnocuousDiagnostic()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var steamCmdPath = System.IO.Path.Combine(directory.Path, "steamcmd.sh");
        var passwordPath = System.IO.Path.Combine(directory.Path, "steam-password");
        const string steamPassword = "adversarial-steam-secret";
        await File.WriteAllTextAsync(steamCmdPath, "test executable placeholder");
        await File.WriteAllTextAsync(passwordPath, steamPassword);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(passwordPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var armaOptions = new Arma3WorkerOptions(
            steamCmdPath,
            "steam-user",
            passwordPath,
            null,
            "arma3server_x64",
            TimeSpan.FromSeconds(30))
        {
            TemporaryDirectory = System.IO.Path.Combine(directory.Path, "tmp"),
        };
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "bootstrap.json"),
            System.IO.Path.Combine(directory.Path, "state"),
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            armaOptions);
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            armaOptions.ExecutableName);
        var runner = new PreparingCommandRunner(
            paths.ExecutablePath,
            $"connection used {steamPassword}; password policy healthy");
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            commandRunner: runner,
            processController: new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath),
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath));

        var result = await executor.ExecuteAsync(
            FlatFileWorkerCommandJournalTests.CreateCommand(
                bootstrap.WorkerId,
                bootstrap.DeploymentId,
                bootstrap.ServerId,
                "install"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(steamPassword, result.Output, StringComparison.Ordinal);
        Assert.Contains("****************", result.Output, StringComparison.Ordinal);
        Assert.Contains("password policy healthy", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectLogs_AfterExecutorRestartReloadsRenderedSecretsBeforeCollectingLogs()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        const string configSecret = "known-config-secret-value";
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "bootstrap.json"),
            System.IO.Path.Combine(directory.Path, "state"),
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                System.IO.Path.Combine(directory.Path, "steamcmd"),
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = System.IO.Path.Combine(directory.Path, "tmp"),
            });
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            options.Arma3.ExecutableName);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ServerConfigurationPath, $"passwordAdmin = \"{configSecret}\";");
        await File.WriteAllTextAsync(paths.BasicConfigurationPath, string.Empty);
        await File.WriteAllTextAsync(paths.BattlEyeConfigurationPath, string.Empty);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(paths.LogPath, "server.rpt"),
            $"authentication rejected {configSecret}; password policy healthy");
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId,
            "collect-logs");
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            processController: new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath),
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath));

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(configSecret, result.Output, StringComparison.Ordinal);
        Assert.Contains("****************", result.Output, StringComparison.Ordinal);
        Assert.Contains("password policy healthy", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FaultStop_AfterExecutorRestartReloadsRenderedSecretsBeforeCollectingLogs()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        const string configSecret = "restart-safe-admin-value";
        var options = new CloudWorkerOptions(
            System.IO.Path.Combine(directory.Path, "bootstrap.json"),
            System.IO.Path.Combine(directory.Path, "state"),
            System.IO.Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                System.IO.Path.Combine(directory.Path, "steamcmd"),
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = System.IO.Path.Combine(directory.Path, "tmp"),
            });
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            options.Arma3.ExecutableName);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ServerConfigurationPath, $"passwordAdmin = \"{configSecret}\";");
        await File.WriteAllTextAsync(paths.BasicConfigurationPath, string.Empty);
        await File.WriteAllTextAsync(paths.BattlEyeConfigurationPath, string.Empty);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(paths.LogPath, "server.rpt"),
            $"authentication rejected {configSecret}; password policy healthy");
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            processController: new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath),
            storageTransfer: new RecordingStorageTransfer(paths.ServerConfigurationPath));

        var result = await executor.ExecuteAsync(
            FlatFileWorkerCommandJournalTests.CreateCommand(
                bootstrap.WorkerId,
                bootstrap.DeploymentId,
                bootstrap.ServerId,
                "fault-stop"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(configSecret, result.Output, StringComparison.Ordinal);
        Assert.Contains("****************", result.Output, StringComparison.Ordinal);
        Assert.Contains("password policy healthy", result.Output, StringComparison.Ordinal);
    }

    private static CloudWorkerOptions CreateOptions(string root, string steamCmdPath)
        => new(
            System.IO.Path.Combine(root, "bootstrap.json"),
            System.IO.Path.Combine(root, "state"),
            System.IO.Path.Combine(root, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                steamCmdPath,
                "anonymous",
                null,
                null,
                "arma3server_x64",
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = System.IO.Path.Combine(root, "tmp"),
            });

    private static WorkerCommandClaimResponse WithContent(
        WorkerCommandClaimResponse command,
        IReadOnlyCollection<WorkerContentReference> content)
    {
        var payload = JsonNode.Parse(command.Payload)?.AsObject()
                      ?? throw new InvalidDataException("Test command payload is invalid.");
        payload[nameof(WorkerCommandPayload.Content)] = JsonSerializer.SerializeToNode(content, CloudWorkerJson.Options);
        return command with { Payload = payload.ToJsonString(CloudWorkerJson.Options) };
    }

    [Fact]
    public async Task Start_HeadlessClient_UsesTopologyAndDoesNotPublishPrimaryStorage()
    {
        using var directory = new TemporaryDirectory();
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap() with
        {
            ServiceIdentifier = "headless-client",
            ServiceOrdinal = 0,
        };
        var executableName = "arma3server_x64";
        var options = new CloudWorkerOptions(
            Path.Combine(directory.Path, "bootstrap.json"),
            Path.Combine(directory.Path, "state"),
            Path.Combine(directory.Path, "runtime"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            true,
            false,
            new Arma3WorkerOptions(
                Path.Combine(directory.Path, "steamcmd.sh"),
                "anonymous",
                null,
                null,
                executableName,
                TimeSpan.FromSeconds(30))
            {
                TemporaryDirectory = Path.Combine(directory.Path, "tmp"),
            });
        var paths = new Arma3RuntimePaths(
            options.RuntimeRoot,
            options.StateDirectory,
            bootstrap.DeploymentId,
            executableName);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ExecutablePath, "prepared");
        await File.WriteAllTextAsync(paths.PreparationMarkerPath, "prepared");
        var processController = new RecordingProcessController(paths.ExecutablePath, paths.ServerConfigurationPath);
        var executor = new Arma3WorkerCommandExecutor(
            bootstrap.ToConnectionConfig(),
            options,
            processController: processController);
        var payload = new WorkerCommandPayload
        {
            ServerId = bootstrap.ServerId,
            DeploymentId = bootstrap.DeploymentId,
            GameIdentifier = "arma3",
            ActionIdentifier = "start",
            ServiceIdentifier = "headless-client",
            ServiceOrdinal = 0,
            IsPrimaryService = false,
            PreserveOnStop = false,
            GracefulStopAttempts = 2,
            GracefulStopTimeoutSeconds = 30,
            Topology =
            [
                new WorkerNodeEndpoint
                {
                    ServiceIdentifier = "game-server",
                    Address = "192.0.2.10",
                    Port = 2302,
                    IsPrimary = true,
                },
                new WorkerNodeEndpoint
                {
                    ServiceIdentifier = "headless-client",
                    Address = "192.0.2.20",
                },
            ],
        };
        var command = new WorkerCommandClaimResponse
        {
            CommandId = Guid.NewGuid(),
            LifecycleJobId = Guid.NewGuid(),
            DeploymentId = bootstrap.DeploymentId,
            WorkerId = bootstrap.WorkerId,
            Sequence = 1,
            ActionIdentifier = "start",
            Payload = JsonSerializer.Serialize(payload, CloudWorkerJson.Options),
            Secrets = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["join-password"] = "hc-join-secret",
            },
            Attempt = 1,
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
        };

        var result = await executor.ExecuteAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("-client", processController.ArgumentsAtStart);
        Assert.Contains("-connect=192.0.2.10", processController.ArgumentsAtStart);
        Assert.Contains("-port=2302", processController.ArgumentsAtStart);
        Assert.Contains("-password=hc-join-secret", processController.ArgumentsAtStart);
        Assert.Contains("-name=UTFHC1", processController.ArgumentsAtStart);
        Assert.DoesNotContain(
            processController.ArgumentsAtStart,
            q => q.StartsWith("-config=", StringComparison.Ordinal));
    }

    private sealed class PreparingCommandRunner(
        string executablePath,
        string output = "prepared",
        IReadOnlyCollection<string>? preparedDirectories = null) : IProcessCommandRunner
    {
        public int RunCount { get; private set; }
        public string? RunScriptPath { get; private set; }
        public bool RunScriptExistedDuringExecution { get; private set; }
        public UnixFileMode? RunScriptMode { get; private set; }
        public string RunScriptContent { get; private set; } = string.Empty;
        public string? HomeDirectory { get; private set; }

        public async Task<ProcessCommandResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            RunCount++;
            HomeDirectory = startInfo.Environment["HOME"];
            RunScriptPath = startInfo.ArgumentList.Last();
            RunScriptExistedDuringExecution = File.Exists(RunScriptPath);
            RunScriptContent = await File.ReadAllTextAsync(RunScriptPath, cancellationToken);
            if (!OperatingSystem.IsWindows())
                RunScriptMode = File.GetUnixFileMode(RunScriptPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(executablePath)!);
            await File.WriteAllTextAsync(executablePath, "prepared", cancellationToken);
            foreach (var directory in preparedDirectories ?? [])
                Directory.CreateDirectory(directory);
            return new ProcessCommandResult(true, 0, output, string.Empty);
        }
    }

    private sealed class FailingCommandRunner : IProcessCommandRunner
    {
        public async Task<ProcessCommandResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            var logs = System.IO.Path.Combine(startInfo.Environment["HOME"]!, "Steam", "logs");
            Directory.CreateDirectory(logs);
            await File.WriteAllTextAsync(
                System.IO.Path.Combine(logs, "console_log.txt"),
                "Steam console diagnostic\ntoken=diagnostic-secret",
                cancellationToken);
            return new ProcessCommandResult(
                false,
                42,
                new string('x', 20_000) + " stdout tail marker",
                "native stderr detail");
        }
    }

    private sealed class RecordingProcessController(
        string executablePath,
        string serverConfigurationPath) : IArma3ProcessController
    {
        public int StartCount { get; private set; }
        public bool ExecutableExistedAtStart { get; private set; }
        public string? ServerConfigurationAtStart { get; private set; }
        public IReadOnlyList<string> ArgumentsAtStart { get; private set; } = [];

        public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Arma3ProcessObservation(false, null, null, null));

        public Task<WorkerCommandExecutionResult> StartAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            StartCount++;
            ArgumentsAtStart = arguments.ToArray();
            ExecutableExistedAtStart = File.Exists(executablePath);
            ServerConfigurationAtStart = File.Exists(serverConfigurationPath)
                ? File.ReadAllText(serverConfigurationPath)
                : null;
            var startedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(WorkerCommandExecutionResult.Success(
                "started",
                processId: 123,
                processStartedAt: startedAt));
        }

        public Task<WorkerCommandExecutionResult> StopAsync(
            bool force,
            TimeSpan timeout,
            int gracefulAttempts,
            CancellationToken cancellationToken)
            => Task.FromResult(WorkerCommandExecutionResult.Success("stopped"));
    }

    private sealed class RecordingStorageTransfer(string serverConfigurationPath) : ICloudWorkerStorageTransfer
    {
        public bool PersistentOverlayResetBeforeConfigurationRender { get; private set; }
        public bool PersistentSnapshotAppliedAfterConfigurationRender { get; private set; }

        public Task ResetPersistentOverlayAsync(
            string deploymentRoot,
            CancellationToken cancellationToken)
        {
            PersistentOverlayResetBeforeConfigurationRender = !File.Exists(serverConfigurationPath);
            return Task.CompletedTask;
        }

        public async Task ApplyPersistentSnapshotAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
        {
            PersistentSnapshotAppliedAfterConfigurationRender = File.Exists(serverConfigurationPath);
            await File.WriteAllTextAsync(serverConfigurationPath, "persistent-server-config", cancellationToken);
        }

        public Task PublishPreservationAsync(
            WorkerCommandClaimResponse command,
            string deploymentRoot,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
