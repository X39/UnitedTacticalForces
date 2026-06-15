using System.Diagnostics;
using System.Text.Json;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Persists and verifies exact process identity so PID reuse is never mistaken for the game server.
/// </summary>
public sealed class Arma3ProcessIdentityStore(Guid serverId, Guid deploymentId, Arma3RuntimePaths paths)
{
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(2);

    public async Task SaveAsync(Arma3ProcessIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.ServerId != serverId || identity.DeploymentId != deploymentId || identity.ProcessId <= 0)
            throw new InvalidDataException("Arma process identity attribution is invalid.");
        paths.EnsureDirectories();
        var json = JsonSerializer.Serialize(identity, CloudWorkerJson.Options);
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(paths.DeploymentRoot, ".utf-arma3-process", ".tmp", json, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, paths.ProcessIdentityPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(paths.ProcessIdentityPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public async Task<Arma3ProcessObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(paths.ProcessIdentityPath))
            return new Arma3ProcessObservation(false, null, null, null);

        Arma3ProcessIdentity? identity;
        try
        {
            await using var stream = File.OpenRead(paths.ProcessIdentityPath);
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
            if ((observedStart - identity.ProcessStartedAt).Duration() > StartTolerance)
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

    public void Delete()
    {
        if (File.Exists(paths.ProcessIdentityPath))
            File.Delete(paths.ProcessIdentityPath);
    }
}
