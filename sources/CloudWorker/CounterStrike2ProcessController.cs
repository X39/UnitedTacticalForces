using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class CounterStrike2ProcessController(
    Guid serverId,
    Guid deploymentId,
    CounterStrike2RuntimePaths paths,
    RestrictedGameProcessLauncher launcher) : ICounterStrike2ProcessController
{
    private const int SigTerm = 15;
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(2);

    public async Task<CounterStrike2ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ProcessIdentityPath))
            return new CounterStrike2ProcessObservation(false, null, null, null);
        CounterStrike2ProcessIdentity? identity;
        try
        {
            await using var stream = File.OpenRead(paths.ProcessIdentityPath);
            identity = await JsonSerializer.DeserializeAsync<CounterStrike2ProcessIdentity>(
                stream, CloudWorkerJson.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new CounterStrike2ProcessObservation(false, null, null, "Persisted CS2 process identity is unreadable.");
        }
        if (identity is null || identity.ServerId != serverId || identity.DeploymentId != deploymentId
            || identity.ProcessId <= 0 || !string.Equals(
                Path.GetFullPath(identity.ExecutablePath),
                Path.GetFullPath(paths.ExecutablePath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return new CounterStrike2ProcessObservation(false, identity, null, "Persisted CS2 process attribution is invalid.");
        try
        {
            var process = Process.GetProcessById(identity.ProcessId);
            if (process.HasExited)
            {
                process.Dispose();
                DeleteIdentity();
                return new CounterStrike2ProcessObservation(false, null, null, null);
            }
            var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            if ((startedAt - identity.ProcessStartedAt).Duration() > StartTolerance)
            {
                process.Dispose();
                return new CounterStrike2ProcessObservation(false, identity, null, "Persisted CS2 PID belongs to another process.");
            }
            return new CounterStrike2ProcessObservation(true, identity, process, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            DeleteIdentity();
            return new CounterStrike2ProcessObservation(false, null, null, null);
        }
        catch (Win32Exception)
        {
            return new CounterStrike2ProcessObservation(false, identity, null, "CS2 process identity could not be verified.");
        }
    }

    public async Task<WorkerCommandExecutionResult> StartAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var existing = await ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Error is not null)
            return WorkerCommandExecutionResult.Failure("start-reconcile-failed", "process-identity-invalid", existing.Error);
        if (existing.IsRunning && existing.Identity is not null)
            return WorkerCommandExecutionResult.Success("already-running", processId: existing.Identity.ProcessId, processStartedAt: existing.Identity.ProcessStartedAt);
        if (!File.Exists(paths.ExecutablePath))
            return WorkerCommandExecutionResult.Failure("start-not-prepared", "cs2-not-installed", "Counter-Strike 2 is not installed.");

        paths.EnsureDirectories();
        Process? process = null;
        try
        {
            process = Process.Start(launcher.Create(paths.ExecutablePath, paths.InstallPath, arguments))
                      ?? throw new InvalidOperationException("Counter-Strike 2 process could not be created.");
            var identity = new CounterStrike2ProcessIdentity(
                serverId,
                deploymentId,
                process.Id,
                new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
                paths.ExecutablePath,
                ReadGamePort(arguments));
            await SaveIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
            if (process.HasExited)
            {
                DeleteIdentity();
                return WorkerCommandExecutionResult.Failure("start-exited", "cs2-exited", $"Counter-Strike 2 exited immediately with code {process.ExitCode}.");
            }
            return WorkerCommandExecutionResult.Success("started", processId: identity.ProcessId, processStartedAt: identity.ProcessStartedAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            if (process is { HasExited: false }) process.Kill(true);
            DeleteIdentity();
            return WorkerCommandExecutionResult.Failure("start-failed", "process-start-failed", WorkerSecretRedactor.Redact(ex.Message) ?? "CS2 process start failed.");
        }
        finally
        {
            process?.Dispose();
        }
    }

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
            var forced = force;
            if (!force)
            {
                var perAttempt = TimeSpan.FromTicks(Math.Max(1, timeout.Ticks / Math.Max(1, attempts)));
                for (var attempt = 0; attempt < Math.Max(1, attempts) && !process.HasExited; attempt++)
                {
                    RequestStop(process);
                    if (await WaitAsync(process, perAttempt, cancellationToken).ConfigureAwait(false)) break;
                }
                forced = !process.HasExited;
            }
            if (forced && !process.HasExited)
            {
                process.Kill(true);
                if (!await WaitAsync(process, timeout, cancellationToken).ConfigureAwait(false))
                    return WorkerCommandExecutionResult.Failure("stop-force-unverified", "process-still-running", "CS2 exit could not be verified.");
            }
            DeleteIdentity();
            return WorkerCommandExecutionResult.Success(forced ? "force-stopped" : "stopped");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return WorkerCommandExecutionResult.Failure("stop-failed", "process-stop-failed", WorkerSecretRedactor.Redact(ex.Message) ?? "CS2 process stop failed.");
        }
    }

    public async Task<WorkerCommandExecutionResult> TerminateAsync(CancellationToken cancellationToken)
        => await StopAsync(true, TimeSpan.FromSeconds(30), 1, cancellationToken).ConfigureAwait(false);

    /// <summary>Reads the game port that follows the <c>-port</c> start argument.</summary>
    /// <param name="arguments">The start arguments of the game process.</param>
    /// <returns>The port from 1 to 65535, or null when the arguments do not carry a valid one.</returns>
    internal static int? ReadGamePort(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == "-port"
                && int.TryParse(arguments[index + 1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var port)
                && port is >= 1 and <= 65535)
                return port;
        }
        return null;
    }

    private async Task SaveIdentityAsync(CounterStrike2ProcessIdentity identity, CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        var json = JsonSerializer.Serialize(identity, CloudWorkerJson.Options);
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            paths.StateRoot, ".utf-cs2-process", ".tmp", json, cancellationToken).ConfigureAwait(false);
        File.Move(temporary.Path, paths.ProcessIdentityPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(paths.ProcessIdentityPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private void DeleteIdentity()
    {
        if (File.Exists(paths.ProcessIdentityPath)) File.Delete(paths.ProcessIdentityPath);
    }

    private static void RequestStop(Process process)
    {
        if (OperatingSystem.IsWindows()) process.CloseMainWindow();
        else if (Kill(process.Id, SigTerm) != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static async Task<bool> WaitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
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

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
