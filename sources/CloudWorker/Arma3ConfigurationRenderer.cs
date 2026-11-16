using System.Text;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Writes typed Arma configuration and reconciles worker-owned admission after restoration.</summary>
public sealed class Arma3ConfigurationRenderer
{
    /// <summary>Renders the selected typed settings into worker-owned configuration targets.</summary>
    /// <param name="payload">The command's resolved game configuration.</param>
    /// <param name="paths">The deployment directories containing generated files.</param>
    /// <param name="cancellationToken">Cancels configuration writes.</param>
    /// <returns>A task completing after all targets have been written.</returns>
    /// <exception cref="InvalidDataException">A target escapes the deployment or settings are invalid.</exception>
    public async Task RenderAsync(
        Arma3CommandPayload payload,
        Arma3RuntimePaths paths,
        CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        var implementation = GameConfigurationImplementations.Get(payload.Command.GameIdentifier);
        foreach (var rendered in implementation.Render(payload.Command.Configuration))
        {
            var path = ResolveTarget(paths.DeploymentRoot, rendered.Path);
            var content = rendered.Content;
            if (payload.Command.IsPrimaryService
                && string.Equals(rendered.Path, "server/server.cfg", StringComparison.Ordinal))
            {
                var headlessAddresses = payload.Command.Topology
                    .Where(q => !q.IsPrimary
                                && string.Equals(q.ServiceIdentifier, "headless-client", StringComparison.Ordinal))
                    .Select(q => q.Address)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                if (headlessAddresses.Length > 0)
                {
                    var suffix = $"headlessClients[] = {{{string.Join(',', headlessAddresses.Select(QuoteArma))}}};"
                                 + Environment.NewLine;
                    content = [.. content, .. Encoding.UTF8.GetBytes(suffix)];
                }
            }
            await WriteRestrictedAtomicAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reapplies current Arma admission permissions after persistent and content overlays.</summary>
    /// <param name="payload">The immutable command containing attributed service endpoints.</param>
    /// <param name="paths">The worker-owned deployment paths.</param>
    /// <param name="cancellationToken">Cancels configuration IO before process launch.</param>
    /// <returns>A task completing after the primary configuration is atomically replaced.</returns>
    /// <exception cref="InvalidDataException">The restored configuration or topology is unsafe to reconcile.</exception>
    public async Task ReconcileAdmissionAsync(
        Arma3CommandPayload payload,
        Arma3RuntimePaths paths,
        CancellationToken cancellationToken)
    {
        if (!payload.Command.IsPrimaryService)
            return;
        var configuration = await ReadAdmissionConfigurationAsync(paths.ServerConfigurationPath, cancellationToken)
            .ConfigureAwait(false);
        var reconciled = Arma3AdmissionConfiguration.Reconcile(configuration, payload.Command.Topology);
        await WriteRestrictedAtomicAsync(
                paths.ServerConfigurationPath,
                Encoding.UTF8.GetBytes(reconciled),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Bounds restored configuration reads before tokenization and atomic replacement.</summary>
    /// <param name="path">The primary server configuration path.</param>
    /// <param name="cancellationToken">Cancels reading the restored configuration.</param>
    /// <returns>The decoded configuration text, including existing comments.</returns>
    /// <exception cref="InvalidDataException">The restored file exceeds the supported configuration size.</exception>
    private static async Task<string> ReadAdmissionConfigurationAsync(string path, CancellationToken cancellationToken)
    {
        const int maximumBytes = 1024 * 1024;
        await using var stream = File.OpenRead(path);
        if (stream.Length > maximumBytes)
            throw new InvalidDataException("Arma configuration is too large for admission reconciliation.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[maximumBytes + 1];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        if (count > maximumBytes || stream.Length > maximumBytes)
            throw new InvalidDataException("Arma configuration is too large for admission reconciliation.");
        return new string(buffer, 0, count);
    }

    /// <summary>Escapes a string using Arma configuration's doubled-quote convention.</summary>
    /// <param name="value">The value to enclose in quotes.</param>
    /// <returns>A quoted Arma string literal.</returns>
    private static string QuoteArma(string value)
        => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>Confines generated configuration writes to the deployment directory.</summary>
    /// <param name="deploymentRoot">The permitted root directory.</param>
    /// <param name="relativePath">The renderer's relative target.</param>
    /// <returns>The normalized absolute target path.</returns>
    /// <exception cref="InvalidDataException">The target escapes the deployment directory.</exception>
    private static string ResolveTarget(string deploymentRoot, string relativePath)
    {
        var root = Path.GetFullPath(deploymentRoot).TrimEnd(Path.DirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidDataException("A generated configuration target escapes the deployment root.");
        }
        return target;
    }

    /// <summary>Replaces a configuration file atomically using a restricted temporary file.</summary>
    /// <param name="path">The final configuration path.</param>
    /// <param name="content">The UTF-8 configuration bytes.</param>
    /// <param name="cancellationToken">Cancels temporary-file creation.</param>
    /// <returns>A task completing after the replacement and permission update.</returns>
    private static async Task WriteRestrictedAtomicAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(
                directory,
                Path.GetFileName(path),
                ".tmp",
                Encoding.UTF8.GetString(content),
                cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, path, true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite);
        }
    }
}
