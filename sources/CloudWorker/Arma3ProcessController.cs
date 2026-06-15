using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class Arma3ProcessController(
    Guid serverId,
    Guid deploymentId,
    Arma3RuntimePaths paths,
    Arma3ProcessIdentityStore identityStore,
    RestrictedGameProcessLauncher processLauncher,
    Func<ProcessStartInfo, Process?>? processStarter = null) : IArma3ProcessController
{
    private const int SigTerm = 15;

    public Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
        => identityStore.ObserveAsync(cancellationToken);

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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                paths.ExecutablePath);
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
            if (process is not null)
                TryKillStartedProcess(process);
            identityStore.Delete();
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
                                       or InvalidOperationException)
        {
            if (process is not null)
                TryKillStartedProcess(process);
            identityStore.Delete();
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
            identityStore.Delete();
            return WorkerCommandExecutionResult.Success("force-stop-requested");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return WorkerCommandExecutionResult.Failure(
                "terminate-failed",
                "process-terminate-failed",
                WorkerSecretRedactor.Redact(ex.Message) ?? "Arma 3 process termination failed.");
        }
    }

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

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
