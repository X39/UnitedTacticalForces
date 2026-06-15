using System.Diagnostics;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

public class Arma3ProcessIdentityStoreTests
{
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
        }
        finally
        {
            if (spawned is { HasExited: false })
                spawned.Kill(entireProcessTree: true);
            spawned?.Dispose();
        }
    }

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
