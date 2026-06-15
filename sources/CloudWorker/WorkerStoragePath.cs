namespace X39.UnitedTacticalForces.CloudWorker;

internal static class WorkerStoragePath
{
    private static readonly HashSet<string> ForbiddenSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ssh",
        ".aws",
        ".azure",
        ".env",
        "appsettings.secret.json",
        "credentials",
        "credentials.json",
        "secrets",
        "secrets.json",
        "private-key",
        "private_key",
        "provider-token",
        "worker-credentials",
    };

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8",
        "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly HashSet<string> WorkerOwnedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "server-instance",
        "logs",
        "state",
        "worker-state",
        "tooling",
        "tools",
        "steamcmd",
        "steamcmd-tools",
    };

    public static string Normalize(string? relativePath, bool allowRoot = true)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            if (allowRoot)
                return string.Empty;
            throw new InvalidDataException("A worker storage path is required.");
        }
        if (relativePath.Length > 2048
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException("A rooted or oversized worker storage path is not allowed.");
        }

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            if (allowRoot)
                return string.Empty;
            throw new InvalidDataException("A worker storage path is required.");
        }
        foreach (var segment in segments)
        {
            if (segment is "." or ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || segment.EndsWith(' ')
                || segment.EndsWith('.')
                || ReservedDeviceNames.Contains(Path.GetFileNameWithoutExtension(segment)))
            {
                throw new InvalidDataException("A worker storage path contains an unsafe file name.");
            }
            if (ForbiddenSegments.Contains(segment)
                || segment.StartsWith(".utf-", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("appsettings.secret.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A worker storage path addresses protected metadata.");
            }
        }
        if (WorkerOwnedRoots.Contains(segments[0]))
            throw new InvalidDataException("A worker storage path addresses worker-owned runtime state.");
        return string.Join('/', segments);
    }

    public static string ResolveWithinRoot(string rootPath, string? relativePath, bool allowRoot = true)
    {
        var root = Path.GetFullPath(rootPath);
        var normalized = Normalize(relativePath, allowRoot);
        var candidate = normalized.Length == 0
            ? root
            : Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.Equals(root, comparison) && !candidate.StartsWith(rootPrefix, comparison))
            throw new InvalidDataException("A worker storage path escapes its deployment root.");
        RejectExistingLinks(root, candidate);
        return candidate;
    }

    public static void RejectExistingLinks(string rootPath, string candidatePath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        var ancestor = Directory.GetParent(root);
        while (ancestor is not null)
        {
            RejectLink(ancestor.FullName);
            ancestor = ancestor.Parent;
        }
        var current = root;
        RejectLink(current);
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".")
            return;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                break;
            RejectLink(current);
        }
    }

    public static void RejectLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path))
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Symbolic links are not accepted by worker storage transfer.");
        }
    }
}
