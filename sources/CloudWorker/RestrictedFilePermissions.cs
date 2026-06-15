namespace X39.UnitedTacticalForces.CloudWorker;

internal static class RestrictedFilePermissions
{
    public static void EnsureOwnerOnlyReadable(string path, string description)
    {
        if (OperatingSystem.IsWindows())
            return;
        var mode = File.GetUnixFileMode(path);
        const UnixFileMode allowed = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        if ((mode & UnixFileMode.UserRead) == 0 || (mode & ~allowed) != 0)
        {
            throw new InvalidDataException(
                $"{description} must be readable only by its owner (Unix mode 0400 or 0600).");
        }
    }
}