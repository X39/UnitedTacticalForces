using System.Diagnostics;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Builds a process start that drops the worker agent identity before any game-controlled executable runs.
/// </summary>
public sealed class RestrictedGameProcessLauncher
{
    private readonly Arma3WorkerOptions _options;
    private readonly bool _isLinux;

    public RestrictedGameProcessLauncher(Arma3WorkerOptions options, bool? isLinux = null)
    {
        _options = options;
        _isLinux = isLinux ?? OperatingSystem.IsLinux();
    }

    public ProcessStartInfo Create(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments)
        => CreateRestricted(
            executablePath,
            workingDirectory,
            arguments,
            _options.GameUserId,
            _options.GameGroupId,
            "utf-game");

    public ProcessStartInfo CreateSteamCmd(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments)
    {
        if (_options.SteamUserId is null && _options.GameUserId is not null)
        {
            throw new InvalidOperationException(
                "SteamCMD requires a dedicated Unix identity when the game identity is restricted.");
        }
        return CreateRestricted(
            executablePath,
            workingDirectory,
            arguments,
            _options.SteamUserId,
            _options.GameGroupId,
            "utf-steam");
    }

    private ProcessStartInfo CreateRestricted(
        string executablePath,
        string workingDirectory,
        IEnumerable<string> arguments,
        int? userId,
        int? groupId,
        string processName)
    {
        var executable = Path.GetFullPath(executablePath);
        var working = Path.GetFullPath(workingDirectory);
        var suppliedArguments = arguments.ToArray();
        if (userId is null && groupId is null)
            return Direct(executable, working, suppliedArguments);
        if (!_isLinux
            || userId is not { } effectiveUserId
            || groupId is not { } effectiveGroupId
            || string.IsNullOrWhiteSpace(_options.PrivilegeDropPath))
        {
            throw new InvalidOperationException(
                "The game process identity is incomplete; both Linux UID/GID and a privilege-drop launcher are required.");
        }

        var privilegeDropPath = Path.GetFullPath(_options.PrivilegeDropPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = privilegeDropPath,
            WorkingDirectory = working,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add($"--reuid={effectiveUserId}");
        startInfo.ArgumentList.Add($"--regid={effectiveGroupId}");
        startInfo.ArgumentList.Add("--clear-groups");
        startInfo.ArgumentList.Add("--no-new-privs");
        startInfo.ArgumentList.Add("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("umask 0007; exec \"$@\"");
        startInfo.ArgumentList.Add(processName);
        startInfo.ArgumentList.Add(executable);
        foreach (var argument in suppliedArguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static ProcessStartInfo Direct(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }
}
