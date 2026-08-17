using System.IO.Compression;

namespace X39.UnitedTacticalForces.CloudWorker;

public static class TrustedArchiveInstaller
{
    private const int MaximumEntries = 10_000;
    private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> SupportedInstallers =
        new(["archive-overlay", "cs2-metamod", "cs2-counterstrikesharp", "cs2-plugin"], StringComparer.Ordinal);

    public static void Install(
        string archivePath,
        string installRoot,
        string installerIdentifier,
        ISet<string> commandTargets)
    {
        if (!SupportedInstallers.Contains(installerIdentifier))
            throw new InvalidDataException("The content installer is not trusted by this worker.");
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
            var relative = Normalize(entry.FullName);
            if (relative.StartsWith("csgo/", StringComparison.OrdinalIgnoreCase))
                relative = "game/" + relative;
            if (!relative.StartsWith("game/csgo/addons/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("CS2 content archives may write only beneath game/csgo/addons.");
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

    private static string Normalize(string path)
    {
        path = path.Replace('\\', '/').TrimStart('/');
        if (path.Length == 0 || path.Split('/').Any(q => q is "" or "." or ".."))
            throw new InvalidDataException("Content archive contains an unsafe path.");
        return path;
    }

    private static bool IsLink(ZipArchiveEntry entry)
    {
        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        return mode == 0xA000;
    }
}
