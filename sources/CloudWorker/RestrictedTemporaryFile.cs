using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Creates sensitive temporary files with owner-only access before any bytes are written.
/// </summary>
internal sealed class RestrictedTemporaryFile : IDisposable
{
    private RestrictedTemporaryFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public void RestrictToUnixOwner(int userId)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Unix process ownership is required for the Steam credential file.");
        if (userId <= 0 || Chown(Path, userId, -1) != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Steam credential ownership could not be isolated.");
        File.SetUnixFileMode(Path, UnixFileMode.UserRead);
    }

    public static async Task<RestrictedTemporaryFile> CreateAsync(
        string directory,
        string prefix,
        string extension,
        string content,
        CancellationToken cancellationToken,
        UnixFileMode? unixFileMode = null)
    {
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, $"{prefix}-{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var stream = CreateRestrictedFile(path))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows() && unixFileMode is { } mode)
                File.SetUnixFileMode(path, mode);
            return new RestrictedTemporaryFile(path);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    public void Dispose() => TryDelete(Path);

    private static FileStream CreateRestrictedFile(string path)
    {
        if (OperatingSystem.IsWindows())
            return CreateRestrictedWindowsFile(path);
        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            Options = FileOptions.Asynchronous,
        });
    }

    [SupportedOSPlatform("windows")]
    private static FileStream CreateRestrictedWindowsFile(string path)
    {
        var security = new FileSecurity();
        var user = WindowsIdentity.GetCurrent().User
                   ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
        security.SetOwner(user);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(path).Create(
            FileMode.CreateNew,
            FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete,
            FileShare.None,
            4096,
            FileOptions.Asynchronous,
            security);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int Chown(string path, int owner, int group);
}