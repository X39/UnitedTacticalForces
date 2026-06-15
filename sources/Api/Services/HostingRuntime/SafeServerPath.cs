namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Normalizes server-relative paths and prevents traversal through host or reparse-point paths.
/// </summary>
public static class SafeServerPath
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
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Normalize(string? relativePath, bool allowRoot = true)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            if (allowRoot)
                return string.Empty;
            throw new ArgumentException("A server-relative path is required.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':', StringComparison.Ordinal))
            throw new ArgumentException("Rooted server file paths are not allowed.", nameof(relativePath));

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            if (allowRoot)
                return string.Empty;
            throw new ArgumentException("A server-relative path is required.", nameof(relativePath));
        }

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
                throw new ArgumentException("Relative path traversal is not allowed.", nameof(relativePath));
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "The server-relative path contains invalid characters.",
                    nameof(relativePath));
            }
            if (segment.EndsWith(' ') || segment.EndsWith('.')
                                      || ReservedDeviceNames.Contains(Path.GetFileNameWithoutExtension(segment)))
            {
                throw new ArgumentException(
                    "The server-relative path contains a reserved file name.",
                    nameof(relativePath));
            }
            if (ForbiddenSegments.Contains(segment)
                || segment.StartsWith(".utf-", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("appsettings.secret.", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The requested path is reserved for protected credentials.",
                    nameof(relativePath));
            }
        }

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
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.Equals(root, comparison) && !candidate.StartsWith(rootPrefix, comparison))
            throw new ArgumentException("The server-relative path escapes its server root.", nameof(relativePath));

        RejectExistingReparsePoints(root, candidate);
        return candidate;
    }

    public static string ResolveConfiguredPath(string contentRootPath, string configuredPath, string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException($"{configurationPath} must be configured.");

        var resolved = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
        return resolved;
    }

    public static void RejectExistingReparsePoints(string rootPath, string candidatePath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        RejectAncestorReparsePoints(root);
        var current = root;
        RejectReparsePoint(current);

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
            RejectReparsePoint(current);
        }
    }

    private static void RejectAncestorReparsePoints(string path)
    {
        var current = Directory.GetParent(path);
        while (current is not null)
        {
            RejectReparsePoint(current.FullName);
            current = current.Parent;
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Symbolic links and other reparse points are not available through server storage.");
    }
}
