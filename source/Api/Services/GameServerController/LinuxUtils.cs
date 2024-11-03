using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

internal static class PathUtils
{

    public static string PathRelativeTo(this string self, string target)
    {
        var pathSplatted = self.Split('/', '\\');
        var targetSplatted = target.Split('/', '\\');
        var matchingPathSegments = 0;
        for (var i = 0; i < targetSplatted.Length; i++)
        {
            if (pathSplatted.Length > i && targetSplatted[i] == pathSplatted[i])
                matchingPathSegments++;
            else
                break;
        }

        var relativeUp = Enumerable.Range(0, targetSplatted.Length - matchingPathSegments).Select((_) => "..");
        var relativeDown = pathSplatted.Skip(matchingPathSegments);
        return Path.Combine(relativeUp.Concat(relativeDown).ToArray());
    }
}
[SupportedOSPlatform("linux")]
internal static partial class LinuxUtils
{
    [LibraryImport("libc", SetLastError = true, EntryPoint = "kill")]
    private static partial int sys_kill(int pid, int sig);

    public static void Kill(this Process process, Signum sig)
    {
        sys_kill(process.Id, (int) sig);
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "CommentTypo")]
    [SuppressMessage("Design", "CA1069")]
    [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
    public enum Signum : int
    {
        SIGHUP    = 1, // Hangup (POSIX).
        SIGINT    = 2, // Interrupt (ANSI).
        SIGQUIT   = 3, // Quit (POSIX).
        SIGILL    = 4, // Illegal instruction (ANSI).
        SIGTRAP   = 5, // Trace trap (POSIX).
        SIGABRT   = 6, // Abort (ANSI).
        SIGIOT    = 6, // IOT trap (4.2 BSD).
        SIGBUS    = 7, // BUS error (4.2 BSD).
        SIGFPE    = 8, // Floating-point exception (ANSI).
        SIGKILL   = 9, // Kill, unblockable (POSIX).
        SIGUSR1   = 10, // User-defined signal 1 (POSIX).
        SIGSEGV   = 11, // Segmentation violation (ANSI).
        SIGUSR2   = 12, // User-defined signal 2 (POSIX).
        SIGPIPE   = 13, // Broken pipe (POSIX).
        SIGALRM   = 14, // Alarm clock (POSIX).
        SIGTERM   = 15, // Termination (ANSI).
        SIGSTKFLT = 16, // Stack fault.
        SIGCLD    = SIGCHLD, // Same as SIGCHLD (System V).
        SIGCHLD   = 17, // Child status has changed (POSIX).
        SIGCONT   = 18, // Continue (POSIX).
        SIGSTOP   = 19, // Stop, unblockable (POSIX).
        SIGTSTP   = 20, // Keyboard stop (POSIX).
        SIGTTIN   = 21, // Background read from tty (POSIX).
        SIGTTOU   = 22, // Background write to tty (POSIX).
        SIGURG    = 23, // Urgent condition on socket (4.2 BSD).
        SIGXCPU   = 24, // CPU limit exceeded (4.2 BSD).
        SIGXFSZ   = 25, // File size limit exceeded (4.2 BSD).
        SIGVTALRM = 26, // Virtual alarm clock (4.2 BSD).
        SIGPROF   = 27, // Profiling alarm clock (4.2 BSD).
        SIGWINCH  = 28, // Window size change (4.3 BSD, Sun).
        SIGPOLL   = SIGIO, // Pollable event occurred (System V).
        SIGIO     = 29, // I/O now possible (4.2 BSD).
        SIGPWR    = 30, // Power failure restart (System V).
        SIGSYS    = 31, // Bad system call.
        SIGUNUSED = 31,
    }
}