using System.Diagnostics;
using System.Text.Json;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Persists and verifies exact process identity so PID reuse is never mistaken for the game server.
/// </summary>
/// <param name="serverId">The durable server identity.</param>
/// <param name="deploymentId">The deployment whose processes may be adopted.</param>
/// <param name="paths">The workload-specific identity and shared installation paths.</param>
public sealed class Arma3ProcessIdentityStore(Guid serverId, Guid deploymentId, Arma3RuntimePaths paths)
{
    /// <summary>Persists launch intent before process creation so an interrupted spawn cannot be repeated blindly.</summary>
    /// <param name="cancellationToken">Cancels the durable write before launch.</param>
    /// <returns>The unique launch attempt identity.</returns>
    /// <exception cref="IOException">A previous launch intent exists or its durable write fails.</exception>
    public async Task<Guid> BeginLaunchAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        var launchId = Guid.NewGuid();
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
                paths.AgentStatePath, ".utf-arma3-launch", ".tmp", launchId.ToString("N"), cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, paths.ProcessLaunchIntentPath, false);
        return launchId;
    }

    /// <summary>Atomically records a process incarnation on the agent-state filesystem.</summary>
    /// <param name="identity">The process evidence attributed to this workload.</param>
    /// <param name="cancellationToken">Cancels the durable write.</param>
    /// <returns>A task completing after the identity has been published.</returns>
    /// <exception cref="InvalidDataException">The process or launch attribution does not match this store.</exception>
    public async Task SaveAsync(Arma3ProcessIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.ServerId != serverId || identity.DeploymentId != deploymentId || identity.ProcessId <= 0
            || identity.ServiceIdentifier != paths.ServiceIdentifier || identity.ServiceOrdinal != paths.ServiceOrdinal
            || !MatchesLaunchIntent(identity))
            throw new InvalidDataException("Arma process identity attribution is invalid.");
        paths.EnsureDirectories();
        var json = JsonSerializer.Serialize(identity, CloudWorkerJson.Options);
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(paths.AgentStatePath, ".utf-arma3-process", ".tmp", json, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, paths.ProcessIdentityPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(paths.ProcessIdentityPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Verifies persisted workload attribution and the current process incarnation without scanning unrelated processes.</summary>
    /// <param name="cancellationToken">Cancels identity loading.</param>
    /// <returns>The verified process, absence, or an attribution failure requiring safe cleanup.</returns>
    public async Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(paths.ProcessIdentityPath))
            return new Arma3ProcessObservation(
                false, null, null, File.Exists(paths.ProcessLaunchIntentPath)
                    ? "An interrupted Arma launch has no verified process identity."
                    : null);

        Arma3ProcessIdentity? identity;
        try
        {
            await using var stream = new FileStream(paths.ProcessIdentityPath, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            identity = await JsonSerializer.DeserializeAsync<Arma3ProcessIdentity>(
                    stream,
                    CloudWorkerJson.Options,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Arma3ProcessObservation(false, null, null, "Persisted Arma process identity is unreadable.");
        }

        string? persistedExecutablePath = null;
        try
        {
            if (identity is not null)
                persistedExecutablePath = Path.GetFullPath(identity.ExecutablePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }
        if (identity is null
            || identity.ServerId != serverId
            || identity.DeploymentId != deploymentId
            || identity.ServiceIdentifier != paths.ServiceIdentifier
            || identity.ServiceOrdinal != paths.ServiceOrdinal
            || !MatchesLaunchIntent(identity)
            || identity.ProcessId <= 0
            || persistedExecutablePath is null
            || !string.Equals(
                persistedExecutablePath,
                Path.GetFullPath(paths.ExecutablePath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return new Arma3ProcessObservation(false, identity, null, "Persisted Arma process attribution is invalid.");
        }

        Process process;
        try
        {
            process = Process.GetProcessById(identity.ProcessId);
            if (process.HasExited)
            {
                process.Dispose();
                Delete();
                return new Arma3ProcessObservation(false, null, null, null);
            }
            var observedStart = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            if (observedStart != identity.ProcessStartedAt)
            {
                process.Dispose();
                return new Arma3ProcessObservation(false, identity, null, "Persisted Arma PID belongs to another process.");
            }
        }
        catch (ArgumentException)
        {
            Delete();
            return new Arma3ProcessObservation(false, null, null, null);
        }
        catch (InvalidOperationException)
        {
            Delete();
            return new Arma3ProcessObservation(false, null, null, null);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new Arma3ProcessObservation(false, identity, null, "Arma process identity could not be verified.");
        }
        return new Arma3ProcessObservation(true, identity, process, null);
    }

    /// <summary>Checks that persisted process evidence belongs to the durable launch attempt.</summary>
    /// <param name="identity">The process incarnation being validated.</param>
    /// <returns>Whether the launch marker agrees, or both records are from the legacy format.</returns>
    private bool MatchesLaunchIntent(Arma3ProcessIdentity identity)
    {
        if (!File.Exists(paths.ProcessLaunchIntentPath))
            return identity.LaunchId is null;
        try
        {
            return Guid.TryParse(File.ReadAllText(paths.ProcessLaunchIntentPath), out var launchId)
                   && identity.LaunchId == launchId;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Removes identity and launch intent only after the caller has verified process exit or absence.</summary>
    public void Delete()
    {
        if (File.Exists(paths.ProcessIdentityPath))
            File.Delete(paths.ProcessIdentityPath);
        if (File.Exists(paths.ProcessLaunchIntentPath))
            File.Delete(paths.ProcessLaunchIntentPath);
    }
}
