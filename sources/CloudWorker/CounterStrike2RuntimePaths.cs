namespace X39.UnitedTacticalForces.CloudWorker;

public sealed class CounterStrike2RuntimePaths
{
    public const int ApplicationId = 730;

    public CounterStrike2RuntimePaths(
        string runtimeRoot,
        string stateRoot,
        Guid deploymentId,
        string executableRelativePath)
    {
        DeploymentRoot = Path.Combine(Path.GetFullPath(runtimeRoot), deploymentId.ToString("N"));
        StateRoot = Path.Combine(Path.GetFullPath(stateRoot), "deployments", deploymentId.ToString("N"));
        InstallPath = Path.Combine(DeploymentRoot, "server-instance");
        ExecutablePath = Path.GetFullPath(Path.Combine(InstallPath, executableRelativePath));
        if (!ExecutablePath.StartsWith(Path.GetFullPath(InstallPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The Counter-Strike 2 executable leaves its install root.");
        ConfigurationPath = Path.Combine(InstallPath, "game", "csgo", "cfg");
        ConfigurationFilePath = Path.Combine(ConfigurationPath, "utf-server.cfg");
        LogPath = Path.Combine(DeploymentRoot, "logs");
        ProcessIdentityPath = Path.Combine(StateRoot, "cs2-process.json");
        PreparationMarkerPath = Path.Combine(StateRoot, "cs2-prepared");
        RuntimeManifestPath = Path.Combine(InstallPath, "steamapps", $"appmanifest_{ApplicationId}.acf");
    }

    public string DeploymentRoot { get; }
    public string StateRoot { get; }
    public string InstallPath { get; }
    public string ExecutablePath { get; }
    public string ConfigurationPath { get; }
    public string ConfigurationFilePath { get; }
    public string LogPath { get; }
    public string ProcessIdentityPath { get; }
    public string PreparationMarkerPath { get; }
    public string RuntimeManifestPath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DeploymentRoot);
        Directory.CreateDirectory(StateRoot);
        Directory.CreateDirectory(InstallPath);
        Directory.CreateDirectory(ConfigurationPath);
        Directory.CreateDirectory(LogPath);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(StateRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                         | UnixFileMode.SetGroup;
            foreach (var path in new[] { DeploymentRoot, InstallPath, ConfigurationPath, LogPath })
                File.SetUnixFileMode(path, shared);
        }
    }
}
