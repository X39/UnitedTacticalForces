using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Runs ASKA in an isolated Wine/Xvfb process session and verifies bounded readiness and shutdown.</summary>
public sealed class AskaProcessController(
    Guid serverId,
    Guid deploymentId,
    AskaRuntimePaths paths,
    AskaWorkerOptions options,
    RestrictedGameProcessLauncher launcher) : IAskaProcessController
{
    private const int SigInt = 2;
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<AskaProcessObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ProcessIdentityPath))
            return new AskaProcessObservation(false, null, null, null);
        AskaProcessIdentity? identity;
        try
        {
            await using var stream = File.OpenRead(paths.ProcessIdentityPath);
            identity = await JsonSerializer.DeserializeAsync<AskaProcessIdentity>(
                stream, CloudWorkerJson.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AskaProcessObservation(false, null, null, "Persisted ASKA process identity is unreadable.");
        }
        if (identity is null || identity.ServerId != serverId || identity.DeploymentId != deploymentId
            || identity.ProcessId <= 0 || !string.Equals(
                Path.GetFullPath(identity.ExecutablePath),
                Path.GetFullPath(paths.ExecutablePath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return new AskaProcessObservation(false, identity, null, "Persisted ASKA process attribution is invalid.");
        try
        {
            var process = Process.GetProcessById(identity.ProcessId);
            if (process.HasExited)
            {
                process.Dispose();
                DeleteIdentity();
                return new AskaProcessObservation(false, null, null, null);
            }
            var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            if ((startedAt - identity.ProcessStartedAt).Duration() > StartTolerance)
            {
                process.Dispose();
                return new AskaProcessObservation(false, identity, null, "Persisted ASKA PID belongs to another process.");
            }
            return new AskaProcessObservation(true, identity, process, null);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            DeleteIdentity();
            return new AskaProcessObservation(false, null, null, null);
        }
        catch (Win32Exception)
        {
            return new AskaProcessObservation(false, identity, null, "ASKA process identity could not be verified.");
        }
    }

    /// <inheritdoc />
    public async Task<WorkerCommandExecutionResult> StartAsync(
        IEnumerable<string> secrets,
        CancellationToken cancellationToken)
    {
        using var existing = await ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Error is not null)
            return WorkerCommandExecutionResult.Failure("start-reconcile-failed", "process-identity-invalid", existing.Error);
        if (existing.IsRunning && existing.Identity is not null)
            return WorkerCommandExecutionResult.Success("already-running", processId: existing.Identity.ProcessId, processStartedAt: existing.Identity.ProcessStartedAt);
        if (!File.Exists(paths.ExecutablePath))
            return WorkerCommandExecutionResult.Failure("start-not-prepared", "aska-not-installed", "ASKA is not installed.");

        paths.EnsureDirectories();
        if (File.Exists(paths.RuntimeLogPath))
            File.Delete(paths.RuntimeLogPath);
        Process? process = null;
        try
        {
            var startInfo = launcher.Create(options.SetSidPath, paths.InstallPath, BuildLaunchArguments());
            startInfo.Environment["WINEPREFIX"] = paths.WinePrefixPath;
            startInfo.Environment["WINEARCH"] = "win64";
            startInfo.Environment["WINEDEBUG"] = "-all";
            startInfo.Environment["HOME"] = paths.DeploymentRoot;
            startInfo.Environment["SteamAppId"] = "1898300";
            process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException("ASKA process could not be created.");
            var identity = new AskaProcessIdentity(
                serverId,
                deploymentId,
                process.Id,
                new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
                paths.ExecutablePath,
                false);
            await SaveIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
            var readiness = await WaitForReadinessAsync(process, secrets, cancellationToken).ConfigureAwait(false);
            if (!readiness.Succeeded)
            {
                await ForceStopAsync(process, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                DeleteIdentity();
                return readiness;
            }
            identity = identity with { Ready = true };
            await SaveIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
            return WorkerCommandExecutionResult.Success(
                "started",
                processId: identity.ProcessId,
                processStartedAt: identity.ProcessStartedAt);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or Win32Exception or InvalidOperationException or SocketException)
        {
            if (process is { HasExited: false })
                process.Kill(true);
            DeleteIdentity();
            return WorkerCommandExecutionResult.Failure(
                "start-failed",
                "process-start-failed",
                WorkerSecretRedactor.Redact(exception.Message, secrets) ?? "ASKA process start failed.");
        }
        catch (OperationCanceledException)
        {
            if (process is { HasExited: false })
                process.Kill(true);
            DeleteIdentity();
            throw;
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
        int attempts,
        CancellationToken cancellationToken)
    {
        using var observation = await ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (observation.Error is not null)
            return WorkerCommandExecutionResult.Failure("stop-reconcile-failed", "process-identity-invalid", observation.Error);
        if (!observation.IsRunning || observation.Process is null)
            return WorkerCommandExecutionResult.Success("already-stopped");
        var process = observation.Process;
        try
        {
            var forced = force || observation.Identity?.Ready is not true;
            if (!forced)
            {
                var perAttempt = TimeSpan.FromTicks(Math.Max(1, timeout.Ticks / Math.Max(1, attempts)));
                for (var attempt = 0; attempt < Math.Max(1, attempts) && !process.HasExited; attempt++)
                {
                    SignalProcessGroup(process.Id, SigInt);
                    if (await WaitAsync(process, perAttempt, cancellationToken).ConfigureAwait(false))
                        break;
                }
                forced = !process.HasExited;
            }
            if (forced && !process.HasExited
                && !await ForceStopAsync(process, timeout, cancellationToken).ConfigureAwait(false))
                return WorkerCommandExecutionResult.Failure("stop-force-unverified", "process-still-running", "ASKA exit could not be verified.");
            DeleteIdentity();
            return WorkerCommandExecutionResult.Success(forced ? "force-stopped" : "stopped");
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return WorkerCommandExecutionResult.Failure(
                "stop-failed",
                "process-stop-failed",
                WorkerSecretRedactor.Redact(exception.Message) ?? "ASKA process stop failed.");
        }
    }

    /// <inheritdoc />
    public Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
        => StopAsync(true, TimeSpan.FromSeconds(30), 1, cancellationToken);

    private IReadOnlyList<string> BuildLaunchArguments()
        =>
        [
            options.XvfbRunPath,
            "-a",
            "--server-args=-screen 0 1024x768x24 -nolisten tcp",
            options.WinePath,
            paths.ExecutablePath,
            "-propertiesPath",
            paths.PropertiesPath,
            "-batchmode",
            "-nographics",
            "-logFile",
            paths.RuntimeLogPath,
        ];

    private async Task<WorkerCommandExecutionResult> WaitForReadinessAsync(
        Process process,
        IEnumerable<string> secrets,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + options.ReadinessTimeout;
        var boundChecks = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                var output = await ReadLogTailAsync(cancellationToken).ConfigureAwait(false);
                return WorkerCommandExecutionResult.Failure(
                    "start-exited",
                    "aska-exited",
                    WorkerSecretRedactor.Redact(output, secrets) ?? $"ASKA exited with code {process.ExitCode}.");
            }
            var log = await ReadLogTailAsync(cancellationToken).ConfigureAwait(false);
            if (ContainsAuthenticationFailure(log))
            {
                return WorkerCommandExecutionResult.Failure(
                    "authentication-failed",
                    "aska-authentication-failed",
                    "ASKA rejected the configured authentication token.");
            }
            boundChecks = IsUdpPortBound(27016) ? boundChecks + 1 : 0;
            if (boundChecks >= 5 && log.Contains("Running game version", StringComparison.OrdinalIgnoreCase))
                return WorkerCommandExecutionResult.Success("ready");
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
        return WorkerCommandExecutionResult.Failure(
            "readiness-timeout",
            "aska-readiness-timeout",
            "ASKA did not become ready before the configured timeout.");
    }

    private async Task SaveIdentityAsync(AskaProcessIdentity identity, CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        var json = JsonSerializer.Serialize(identity, CloudWorkerJson.Options);
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            paths.AgentStateRoot, ".utf-aska-process", ".tmp", json, cancellationToken).ConfigureAwait(false);
        File.Move(temporary.Path, paths.ProcessIdentityPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(paths.ProcessIdentityPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private async Task<string> ReadLogTailAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.RuntimeLogPath))
            return string.Empty;
        await using var stream = new FileStream(
            paths.RuntimeLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 16 * 1024)
            stream.Seek(-16 * 1024, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool ContainsAuthenticationFailure(string value)
        => value.Contains("CustomAuthenticationFailed", StringComparison.OrdinalIgnoreCase)
           || value.Contains("authentication failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsUdpPortBound(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return false;
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AddressAlreadyInUse
                                                    or SocketError.AccessDenied)
        {
            return true;
        }
    }

    private static async Task<bool> ForceStopAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!process.HasExited)
            process.Kill(true);
        return await WaitAsync(process, timeout, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> WaitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(source.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }

    private static void SignalProcessGroup(int processId, int signal)
    {
        if (OperatingSystem.IsWindows())
            throw new NotSupportedException("ASKA Wine process-group signaling requires Linux.");
        if (Kill(-processId, signal) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private void DeleteIdentity()
    {
        if (File.Exists(paths.ProcessIdentityPath))
            File.Delete(paths.ProcessIdentityPath);
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
