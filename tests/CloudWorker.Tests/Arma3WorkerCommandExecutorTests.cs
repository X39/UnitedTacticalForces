using System.Diagnostics;
using System.Text.Json.Nodes;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace CloudWorker.Tests;

public class Arma3WorkerCommandExecutorTests
{
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

    private sealed class PreparingCommandRunner(string executablePath, string output = "prepared") : IProcessCommandRunner
    {
        public int RunCount { get; private set; }
        public string? RunScriptPath { get; private set; }
        public bool RunScriptExistedDuringExecution { get; private set; }
        public UnixFileMode? RunScriptMode { get; private set; }

        public async Task<ProcessCommandResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            RunCount++;
            RunScriptPath = startInfo.ArgumentList.Last();
            RunScriptExistedDuringExecution = File.Exists(RunScriptPath);
            if (!OperatingSystem.IsWindows())
                RunScriptMode = File.GetUnixFileMode(RunScriptPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(executablePath)!);
            await File.WriteAllTextAsync(executablePath, "prepared", cancellationToken);
            return new ProcessCommandResult(true, 0, output, string.Empty);
        }
    }

    private sealed class RecordingProcessController(
        string executablePath,
        string serverConfigurationPath) : IArma3ProcessController
    {
        public int StartCount { get; private set; }
        public bool ExecutableExistedAtStart { get; private set; }
        public string? ServerConfigurationAtStart { get; private set; }

        public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Arma3ProcessObservation(false, null, null, null));

        public Task<WorkerCommandExecutionResult> StartAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            StartCount++;
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
