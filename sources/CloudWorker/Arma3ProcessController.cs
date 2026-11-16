using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Starts and stops only process incarnations attributed to one Arma workload.</summary>
/// <param name="serverId">The durable server identity.</param>
/// <param name="deploymentId">The deployment that owns this workload.</param>
/// <param name="paths">The workload-specific runtime and identity paths.</param>
/// <param name="identityStore">The durable process identity verifier.</param>
/// <param name="processLauncher">Creates restricted game-process launch settings.</param>
/// <param name="processStarter">An optional process creation adapter used by focused tests.</param>
public sealed class Arma3ProcessController(
    Guid serverId,
    Guid deploymentId,
    Arma3RuntimePaths paths,
    Arma3ProcessIdentityStore identityStore,
    RestrictedGameProcessLauncher processLauncher,
    Func<ProcessStartInfo, Process?>? processStarter = null) : IArma3ProcessController
{
    /// <summary>The Unix signal requesting graceful process termination.</summary>
    private const int SigTerm = 15;

    /// <inheritdoc />
    public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
        => identityStore.ObserveAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<WorkerCommandExecutionResult> StartAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var observation = await identityStore.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
            return WorkerCommandExecutionResult.Failure("start-reconcile-failed", "process-identity-invalid", observation.Error);
        if (observation.IsRunning && observation.Identity is not null)
        {
            return WorkerCommandExecutionResult.Success(
                "already-running",
                processId: observation.Identity.ProcessId,
                processStartedAt: observation.Identity.ProcessStartedAt);
        }
        if (!File.Exists(paths.ExecutablePath))
        {
            return WorkerCommandExecutionResult.Failure(
                "start-not-prepared",
                "arma-not-installed",
                "Arma 3 dedicated-server files are not installed on this worker.");
        }

        paths.EnsureDirectories();
        var startInfo = processLauncher.Create(paths.ExecutablePath, paths.InstallPath, arguments);

        Process? process = null;
        var spawnAttempted = false;
        Guid? launchId = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            launchId = await identityStore.BeginLaunchAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            spawnAttempted = true;
            process = (processStarter is null ? Process.Start(startInfo) : processStarter(startInfo))
                      ?? throw new InvalidOperationException("Arma 3 process could not be created.");
            using var cancellationRegistration = cancellationToken.Register(
                static state => TryKillStartedProcess((Process)state!),
                process);
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            var identity = new Arma3ProcessIdentity(
                serverId,
                deploymentId,
                process.Id,
                startedAt,
                paths.ExecutablePath,
                paths.ServiceIdentifier,
                paths.ServiceOrdinal,
                launchId);
            await identityStore.SaveAsync(identity, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                identityStore.Delete();
                return WorkerCommandExecutionResult.Failure(
                    "start-exited",
                    "arma-exited",
                    $"Arma 3 exited immediately with code {process.ExitCode}.");
            }
            return WorkerCommandExecutionResult.Success(
                "started",
                processId: identity.ProcessId,
                processStartedAt: identity.ProcessStartedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CleanupFailedLaunchAsync(process, spawnAttempted, launchId.HasValue).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
                                       or InvalidOperationException)
        {
            await CleanupFailedLaunchAsync(process, spawnAttempted, launchId.HasValue).ConfigureAwait(false);
            return WorkerCommandExecutionResult.Failure(
                "start-failed",
                "process-start-failed",
                WorkerSecretRedactor.Redact(ex.Message) ?? "Arma 3 process start failed.");
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<WorkerCommandExecutionResult> StopAsync(
        bool force,
        TimeSpan timeout,
        int gracefulAttempts,
        CancellationToken cancellationToken)
    {
        using var observation = await identityStore.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
            return WorkerCommandExecutionResult.Failure("stop-reconcile-failed", "process-identity-invalid", observation.Error);
        if (!observation.IsRunning || observation.Process is null)
            return WorkerCommandExecutionResult.Success("already-stopped");

        var process = observation.Process;
        try
        {
            var forced = force;
            if (!force)
            {
                var attempts = Math.Max(1, gracefulAttempts);
                var perAttempt = TimeSpan.FromTicks(Math.Max(1, timeout.Ticks / attempts));
                for (var attempt = 0; attempt < attempts && !process.HasExited; attempt++)
                {
                    RequestGracefulStop(process);
                    if (await WaitForExitAsync(process, perAttempt, cancellationToken).ConfigureAwait(false))
                        break;
                }
                forced = !process.HasExited;
            }
            if (forced && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!await WaitForExitAsync(process, timeout, cancellationToken).ConfigureAwait(false))
                {
                    return WorkerCommandExecutionResult.Failure(
                        "stop-force-unverified",
                        "process-still-running",
                        "Arma 3 process exit could not be verified after force termination.");
                }
            }
            identityStore.Delete();
            return WorkerCommandExecutionResult.Success(forced ? "force-stopped" : "stopped");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return WorkerCommandExecutionResult.Failure(
                "stop-failed",
                "process-stop-failed",
                WorkerSecretRedactor.Redact(ex.Message) ?? "Arma 3 process stop failed.");
        }
    }

    /// <inheritdoc />
    public async Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
    {
        using var observation = await identityStore.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
        {
            return WorkerCommandExecutionResult.Failure(
                "terminate-reconcile-failed",
                "process-identity-invalid",
                observation.Error);
        }
        if (!observation.IsRunning || observation.Process is null)
            return WorkerCommandExecutionResult.Success("already-stopped");

        try
        {
            if (!observation.Process.HasExited)
                observation.Process.Kill(entireProcessTree: true);
            if (!await WaitForExitAsync(observation.Process, TimeSpan.FromSeconds(30), cancellationToken)
                    .ConfigureAwait(false))
            {
                return WorkerCommandExecutionResult.Failure(
                    "terminate-unverified", "process-still-running", "Arma process exit could not be verified.");
            }
            identityStore.Delete();
            return WorkerCommandExecutionResult.Success("force-stopped");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return WorkerCommandExecutionResult.Failure(
                "terminate-failed",
                "process-terminate-failed",
                WorkerSecretRedactor.Redact(ex.Message) ?? "Arma 3 process termination failed.");
        }
    }

    /// <summary>Retains durable attribution unless a failed launch is known to have no surviving process.</summary>
    /// <param name="process">The process handle returned by a completed spawn, if available.</param>
    /// <param name="spawnAttempted">Whether process creation may already have happened.</param>
    /// <param name="ownsLaunchIntent">Whether this invocation successfully created its own durable intent.</param>
    /// <returns>A task completing after bounded local cleanup.</returns>
    private async Task CleanupFailedLaunchAsync(Process? process, bool spawnAttempted, bool ownsLaunchIntent)
    {
        if (process is null)
        {
            if (!spawnAttempted && ownsLaunchIntent)
                identityStore.Delete();
            return;
        }
        TryKillStartedProcess(process);
        try
        {
            if (await WaitForExitAsync(process, TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false))
                identityStore.Delete();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Preserve the launch marker when exit cannot be verified, even if identity saving was interrupted.
        }
    }

    /// <summary>Requests a platform-supported graceful shutdown of the verified process.</summary>
    /// <param name="process">The verified process to stop.</param>
    /// <exception cref="Win32Exception">The operating system rejected the stop request.</exception>
    private static void RequestGracefulStop(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            process.CloseMainWindow();
            return;
        }
        if (Kill(process.Id, SigTerm) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Makes a best-effort kill request for a process handle created by this launch.</summary>
    /// <param name="process">The process created by the current operation.</param>
    private static void TryKillStartedProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Cancellation cleanup is best-effort here; the control plane independently tears down provider cost.
        }
    }

    /// <summary>Verifies process exit within a bounded interval.</summary>
    /// <param name="process">The attributed process to observe.</param>
    /// <param name="timeout">The maximum wait, or a default when nonpositive.</param>
    /// <param name="cancellationToken">Cancels the wait without discarding process attribution.</param>
    /// <returns>Whether exit was verified.</returns>
    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
            return true;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }

    /// <summary>Dispatches a Unix signal to a verified process identifier.</summary>
    /// <param name="pid">The process receiving the signal.</param>
    /// <param name="signal">The Unix signal number.</param>
    /// <returns>Zero on success, or an operating-system error result.</returns>
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
