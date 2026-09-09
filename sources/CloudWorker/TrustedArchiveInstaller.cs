using System.IO.Compression;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Applies bounded provider content without permitting path or link escapes.</summary>
public static class TrustedArchiveInstaller
{
    private const int MaximumEntries = 10_000;
    private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> SupportedInstallers =
        new(["archive-overlay", "cs2-metamod", "cs2-counterstrikesharp", "cs2-plugin"], StringComparer.Ordinal);

    /// <summary>Installs a ZIP archive through a registered game installer.</summary>
    /// <param name="archivePath">Validated downloaded archive.</param>
    /// <param name="installRoot">Worker-owned game installation root.</param>
    /// <param name="gameIdentifier">Registered game identifier.</param>
    /// <param name="installerIdentifier">Registered game installer identifier.</param>
    /// <param name="commandTargets">Paths already written by this command.</param>
    /// <param name="relativeRoot">Optional worker-owned isolation directory beneath the install root.</param>
    public static void Install(
        string archivePath,
        string installRoot,
        string gameIdentifier,
        string installerIdentifier,
        ISet<string> commandTargets,
        string? relativeRoot = null)
    {
        ValidateInstaller(gameIdentifier, installerIdentifier);
        archivePath = Path.GetFullPath(archivePath);
        installRoot = Path.GetFullPath(installRoot);
        if (!File.Exists(archivePath) || (File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The staged content archive is unavailable or unsafe.");

        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumEntries)
            throw new InvalidDataException("The content archive contains too many entries.");
        long expanded = 0;
        var planned = new List<(ZipArchiveEntry Entry, string Target)>();
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                continue;
            if (IsLink(entry))
                throw new InvalidDataException("Content archives may not contain links.");
            expanded = checked(expanded + entry.Length);
            if (expanded > MaximumExpandedBytes)
                throw new InvalidDataException("The expanded content archive is too large.");
            var relative = ApplyRelativeRoot(MapRelativePath(gameIdentifier, entry.FullName), relativeRoot);
            var target = Path.GetFullPath(Path.Combine(installRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(installRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Content archive entry leaves the install root.");
            if (!commandTargets.Add(target))
                throw new InvalidDataException("Two selected Mods write the same runtime path.");
            planned.Add((entry, target));
        }

        foreach (var (entry, target) in planned)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            WorkerStoragePath.RejectExistingLinks(installRoot, Path.GetDirectoryName(target)!);
            WorkerStoragePath.RejectLink(target);
            var temporary = target + ".utf-install-" + Guid.NewGuid().ToString("N");
            try
            {
                entry.ExtractToFile(temporary, true);
                File.Move(temporary, target, true);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    /// <summary>Installs a checked-out Git work tree through a registered game installer.</summary>
    /// <param name="sourceRoot">Validated work tree without repository metadata.</param>
    /// <param name="installRoot">Worker-owned game installation root.</param>
    /// <param name="gameIdentifier">Registered game identifier.</param>
    /// <param name="installerIdentifier">Registered game installer identifier.</param>
    /// <param name="commandTargets">Paths already written by this command.</param>
    /// <param name="relativeRoot">Optional worker-owned isolation directory beneath the install root.</param>
    public static void InstallDirectory(
        string sourceRoot,
        string installRoot,
        string gameIdentifier,
        string installerIdentifier,
        ISet<string> commandTargets,
        string? relativeRoot = null)
    {
        ValidateInstaller(gameIdentifier, installerIdentifier);
        sourceRoot = Path.GetFullPath(sourceRoot);
        installRoot = Path.GetFullPath(installRoot);
        WorkerStoragePath.RejectExistingLinks(sourceRoot, sourceRoot);
        var planned = new List<(string Source, string Target)>();
        foreach (var source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            WorkerStoragePath.RejectLink(source);
            var relative = ApplyRelativeRoot(
                MapRelativePath(gameIdentifier, Path.GetRelativePath(sourceRoot, source)),
                relativeRoot);
            var target = Path.GetFullPath(Path.Combine(installRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(installRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Git content leaves the install root.");
            if (!commandTargets.Add(target))
                throw new InvalidDataException("Two selected Mods write the same runtime path.");
            planned.Add((source, target));
        }
        foreach (var (source, target) in planned)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            WorkerStoragePath.RejectExistingLinks(installRoot, Path.GetDirectoryName(target)!);
            var temporary = target + ".utf-install-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(source, temporary, true);
                File.Move(temporary, target, true);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    private static void ValidateInstaller(string gameIdentifier, string installerIdentifier)
    {
        var gameSupportsInstaller = gameIdentifier switch
        {
            "arma3" => installerIdentifier == "archive-overlay",
            "cs2" => installerIdentifier is "archive-overlay" or "cs2-metamod" or "cs2-counterstrikesharp" or "cs2-plugin",
            _ => false,
        };
        if (!SupportedInstallers.Contains(installerIdentifier) || !gameSupportsInstaller)
            throw new InvalidDataException("The content installer is not trusted for this game.");
    }

    private static string MapRelativePath(string gameIdentifier, string path)
    {
        var relative = Normalize(path);
        if (!string.Equals(gameIdentifier, "cs2", StringComparison.OrdinalIgnoreCase)) return relative;
        if (relative.StartsWith("csgo/", StringComparison.OrdinalIgnoreCase)) relative = "game/" + relative;
        if (!relative.StartsWith("game/csgo/addons/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CS2 content may write only beneath game/csgo/addons.");
        return relative;
    }

    private static string ApplyRelativeRoot(string relative, string? relativeRoot)
        => string.IsNullOrWhiteSpace(relativeRoot)
            ? relative
            : Normalize(relativeRoot) + "/" + relative;

    private static string Normalize(string path)
    {
        path = path.Replace('\\', '/').TrimStart('/');
        if (path.Length == 0
            || path.Split('/').Any(q => q is "" or "." or ".." || q.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Content archive contains an unsafe path.");
        return path;
    }

    private static bool IsLink(ZipArchiveEntry entry)
    {
        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        return mode == 0xA000;
    }
}
