using System.Diagnostics;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Verifies workload attribution and recovery at process creation and shutdown boundaries.</summary>
public class Arma3ProcessIdentityStoreTests
{
    /// <summary>Cancellation after spawning cannot leave an untracked live process.</summary>
    /// <returns>A task completing after the process lifecycle assertions.</returns>
    [Fact]
    public async Task StartAsync_CancellationAtSpawnBoundaryKillsProcessAndRemovesIdentity()
    {
        using var directory = new TemporaryDirectory();
        var serverId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var paths = new Arma3RuntimePaths(
            Path.Combine(directory.Path, "runtime"),
            Path.Combine(directory.Path, "state"),
            deploymentId,
            "arma3server_x64");
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ExecutablePath, "test executable placeholder");
        var store = new Arma3ProcessIdentityStore(serverId, deploymentId, paths);
        var options = new Arma3WorkerOptions(
            Path.Combine(directory.Path, "steamcmd"),
            "anonymous",
            null,
            null,
            "arma3server_x64",
            TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource();
        Process? spawned = null;
        var controller = new Arma3ProcessController(
            serverId,
            deploymentId,
            paths,
            store,
            new RestrictedGameProcessLauncher(options, isLinux: false),
            _ =>
            {
                Assert.True(File.Exists(paths.ProcessLaunchIntentPath));
                spawned = StartLongRunningProcess();
                var controllerOwnedProcess = Process.GetProcessById(spawned.Id);
                cancellation.Cancel();
                return controllerOwnedProcess;
            });

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => controller.StartAsync([], cancellation.Token));

            Assert.NotNull(spawned);
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await spawned.WaitForExitAsync(exitTimeout.Token);
            Assert.True(spawned.HasExited);
            Assert.False(File.Exists(paths.ProcessIdentityPath));
            Assert.False(File.Exists(paths.ProcessLaunchIntentPath));
        }
        finally
        {
            if (spawned is { HasExited: false })
                spawned.Kill(entireProcessTree: true);
            spawned?.Dispose();
        }
    }

    /// <summary>PID reuse cannot substitute a different process incarnation.</summary>
    /// <returns>A task completing after the process lifecycle assertions.</returns>
    [Fact]
    public async Task ObserveAsync_VerifiesProcessStartTimeBeforeAdoptingPid()
    {
        using var directory = new TemporaryDirectory();
        var serverId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var paths = new Arma3RuntimePaths(
            Path.Combine(directory.Path, "runtime"),
            Path.Combine(directory.Path, "state"),
            deploymentId,
            "arma3server_x64");
        var store = new Arma3ProcessIdentityStore(serverId, deploymentId, paths);
        using var current = Process.GetCurrentProcess();
        var actualStart = new DateTimeOffset(current.StartTime.ToUniversalTime(), TimeSpan.Zero);
        await store.SaveAsync(new Arma3ProcessIdentity(
            serverId,
            deploymentId,
            current.Id,
            actualStart,
            paths.ExecutablePath), CancellationToken.None);

        using var observed = await store.ObserveAsync(CancellationToken.None);

        Assert.True(observed.IsRunning);
        Assert.Null(observed.Error);

        await store.SaveAsync(new Arma3ProcessIdentity(
            serverId,
            deploymentId,
            current.Id,
            actualStart.AddMinutes(-5),
            paths.ExecutablePath), CancellationToken.None);
        using var reusedPid = await store.ObserveAsync(CancellationToken.None);
        Assert.False(reusedPid.IsRunning);
        Assert.Contains("another process", reusedPid.Error, StringComparison.Ordinal);
    }

    /// <summary>Two colocated workloads share content but never profile, log, or identity files.</summary>
    [Fact]
    public void Paths_IsolateMutableWorkloadStateAndShareInstallation()
    {
        using var directory = new TemporaryDirectory();
        var deploymentId = Guid.NewGuid();
        var primary = new Arma3RuntimePaths(directory.Path, directory.Path, deploymentId, "arma3server_x64");
        var client = new Arma3RuntimePaths(
            directory.Path, directory.Path, deploymentId, "arma3server_x64", "headless-client", 0);

        Assert.Equal(primary.InstallPath, client.InstallPath);
        Assert.Equal(primary.PreparationMarkerPath, client.PreparationMarkerPath);
        Assert.Equal(primary.SteamWorkshopContentPath, client.SteamWorkshopContentPath);
        Assert.NotEqual(primary.ProfilePath, client.ProfilePath);
        Assert.NotEqual(primary.RuntimeLogPath, client.RuntimeLogPath);
        Assert.NotEqual(primary.ProcessIdentityPath, client.ProcessIdentityPath);
        Assert.NotEqual(primary.ProcessLaunchIntentPath, client.ProcessLaunchIntentPath);
        Assert.EndsWith(Path.Combine("server", "profiles"), primary.ProfilePath);
        Assert.Throws<ArgumentException>(() => new Arma3RuntimePaths(
            directory.Path, directory.Path, deploymentId, "arma3server_x64", "../other"));
    }

    /// <summary>Role-swapped identity files cannot cause adoption of the other local workload.</summary>
    /// <returns>A task completing after the process lifecycle assertions.</returns>
    [Fact]
    public async Task ObserveAsync_RejectsIdentityCopiedFromAnotherWorkload()
    {
        using var directory = new TemporaryDirectory();
        var serverId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var primary = new Arma3RuntimePaths(directory.Path, directory.Path, deploymentId, "arma3server_x64");
        var client = new Arma3RuntimePaths(
            directory.Path, directory.Path, deploymentId, "arma3server_x64", "headless-client", 0);
        var primaryStore = new Arma3ProcessIdentityStore(serverId, deploymentId, primary);
        var clientStore = new Arma3ProcessIdentityStore(serverId, deploymentId, client);
        using var process = Process.GetCurrentProcess();
        var identity = new Arma3ProcessIdentity(serverId, deploymentId, process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero), primary.ExecutablePath);
        await primaryStore.SaveAsync(identity, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => clientStore.SaveAsync(identity, CancellationToken.None));
        File.Copy(primary.ProcessIdentityPath, client.ProcessIdentityPath);

        using var observation = await clientStore.ObserveAsync(CancellationToken.None);
        Assert.False(observation.IsRunning);
        Assert.NotNull(observation.Error);
        Assert.True(File.Exists(client.ProcessIdentityPath));
    }

    /// <summary>A crash between spawn intent and identity publication prevents a duplicate launch.</summary>
    /// <returns>A task completing after the process lifecycle assertions.</returns>
    [Fact]
    public async Task StartAsync_UnresolvedLaunchIntentNeverCreatesAnotherProcess()
    {
        using var directory = new TemporaryDirectory();
        var serverId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var paths = new Arma3RuntimePaths(directory.Path, directory.Path, deploymentId, "arma3server_x64");
        var store = new Arma3ProcessIdentityStore(serverId, deploymentId, paths);
        await store.BeginLaunchAsync(CancellationToken.None);
        var options = new Arma3WorkerOptions(
            "steamcmd", "anonymous", null, null, "arma3server_x64", TimeSpan.FromSeconds(30));
        var spawned = false;
        var controller = new Arma3ProcessController(serverId, deploymentId, paths, store,
            new RestrictedGameProcessLauncher(options, isLinux: false), _ =>
            {
                spawned = true;
                return null;
            });

        var outcome = await controller.StartAsync([], CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal("process-identity-invalid", outcome.ErrorCode);
        Assert.False(spawned);
        Assert.True(File.Exists(paths.ProcessLaunchIntentPath));
        var termination = await controller.TerminateAsync(CancellationToken.None);
        Assert.False(termination.Succeeded);
        Assert.True(File.Exists(paths.ProcessLaunchIntentPath));
    }

    /// <summary>Only a matching launch attempt can publish and recover a current identity.</summary>
    /// <returns>A task completing after the process lifecycle assertions.</returns>
    [Fact]
    public async Task SaveAsync_RequiresMatchingLaunchGeneration()
    {
        using var directory = new TemporaryDirectory();
        var serverId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var paths = new Arma3RuntimePaths(directory.Path, directory.Path, deploymentId,
            "arma3server_x64", "headless-client", 0);
        var store = new Arma3ProcessIdentityStore(serverId, deploymentId, paths);
        var launchId = await store.BeginLaunchAsync(CancellationToken.None);
        using var process = Process.GetCurrentProcess();
        var identity = new Arma3ProcessIdentity(serverId, deploymentId, process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero), paths.ExecutablePath,
            "headless-client", 0, launchId);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
            identity with { LaunchId = Guid.NewGuid() }, CancellationToken.None));
        await store.SaveAsync(identity, CancellationToken.None);

        using var observation = await store.ObserveAsync(CancellationToken.None);
        Assert.True(observation.IsRunning);
        Assert.Null(observation.Error);
        Assert.Equal(launchId, observation.Identity!.LaunchId);
    }

    /// <summary>Termination reports success and deletes attribution only after actual exit.</summary>
    /// <returns>A task completing after the process lifecycle assertions.</returns>
    [Fact]
    public async Task TerminateAsync_VerifiesExitBeforeDeletingIdentity()
    {
        using var directory = new TemporaryDirectory();
        var serverId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        var paths = new Arma3RuntimePaths(directory.Path, directory.Path, deploymentId, "arma3server_x64");
        var store = new Arma3ProcessIdentityStore(serverId, deploymentId, paths);
        using var process = StartLongRunningProcess();
        try
        {
            var launchId = await store.BeginLaunchAsync(CancellationToken.None);
            await store.SaveAsync(new Arma3ProcessIdentity(serverId, deploymentId, process.Id,
                new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero), paths.ExecutablePath,
                LaunchId: launchId), CancellationToken.None);
            var options = new Arma3WorkerOptions(
                "steamcmd", "anonymous", null, null, "arma3server_x64", TimeSpan.FromSeconds(30));
            var controller = new Arma3ProcessController(serverId, deploymentId, paths, store,
                new RestrictedGameProcessLauncher(options, isLinux: false));

            var outcome = await controller.TerminateAsync(CancellationToken.None);

            Assert.True(outcome.Succeeded);
            Assert.True(process.HasExited);
            Assert.False(File.Exists(paths.ProcessIdentityPath));
            Assert.False(File.Exists(paths.ProcessLaunchIntentPath));
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    /// <summary>Starts a harmless child process for bounded lifecycle verification.</summary>
    /// <returns>The child process handle, owned by the test.</returns>
    /// <exception cref="InvalidOperationException">The test process could not be created.</exception>
    private static Process StartLongRunningProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows()
                ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
                : "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("ping -n 60 127.0.0.1 > nul");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("sleep 60");
        }
        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("Test process could not be started.");
    }
}
