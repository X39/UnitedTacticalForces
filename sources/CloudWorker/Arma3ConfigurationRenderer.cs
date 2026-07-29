using System.Text;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class Arma3ConfigurationRenderer
{
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
            await WriteRestrictedAtomicAsync(path, rendered.Content, cancellationToken).ConfigureAwait(false);
        }
    }

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
