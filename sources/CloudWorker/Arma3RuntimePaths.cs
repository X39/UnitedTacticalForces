namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Worker-owned structural paths for one deployment. No command payload may replace them.
/// </summary>
public sealed class Arma3RuntimePaths
{
    public const int DedicatedServerAppId = 233780;

    public Arma3RuntimePaths(
        string runtimeBasePath,
        string agentStateBasePath,
        Guid deploymentId,
        string executableName)
    {
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("Deployment identity cannot be empty.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(executableName)
            || executableName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Arma executable name must be a file name.", nameof(executableName));
        }
        RuntimeBasePath = Path.GetFullPath(runtimeBasePath);
        AgentStateBasePath = Path.GetFullPath(agentStateBasePath);
        DeploymentRoot = Path.Combine(RuntimeBasePath, deploymentId.ToString("N"));
        AgentStatePath = Path.Combine(AgentStateBasePath, "deployments", deploymentId.ToString("N"));
        InstallPath = Path.Combine(DeploymentRoot, "server-instance");
        ConfigurationPath = Path.Combine(DeploymentRoot, "server");
        ProfilePath = Path.Combine(ConfigurationPath, "profiles");
        BattlEyePath = Path.Combine(ConfigurationPath, "battleye");
        LogPath = Path.Combine(DeploymentRoot, "logs");
        ExecutablePath = Path.Combine(InstallPath, executableName);
        ServerConfigurationPath = Path.Combine(ConfigurationPath, "server.cfg");
        BasicConfigurationPath = Path.Combine(ConfigurationPath, "basic.cfg");
        BattlEyeConfigurationPath = Path.Combine(BattlEyePath, "beserver.cfg");
        ProcessIdentityPath = Path.Combine(AgentStatePath, "arma3-process.json");
        PreparationMarkerPath = Path.Combine(AgentStatePath, "arma3-prepared");
        RuntimeLogPath = Path.Combine(LogPath, "arma3-runtime.log");
    }

    public string RuntimeBasePath { get; }
    public string AgentStateBasePath { get; }
    public string DeploymentRoot { get; }
    public string AgentStatePath { get; }
    public string InstallPath { get; }
    public string ConfigurationPath { get; }
    public string ProfilePath { get; }
    public string BattlEyePath { get; }
    public string LogPath { get; }
    public string ExecutablePath { get; }
    public string ServerConfigurationPath { get; }
    public string BasicConfigurationPath { get; }
    public string BattlEyeConfigurationPath { get; }
    public string ProcessIdentityPath { get; }
    public string PreparationMarkerPath { get; }
    public string RuntimeLogPath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RuntimeBasePath);
        Directory.CreateDirectory(AgentStatePath);
        Directory.CreateDirectory(DeploymentRoot);
        Directory.CreateDirectory(InstallPath);
        Directory.CreateDirectory(ConfigurationPath);
        Directory.CreateDirectory(ProfilePath);
        Directory.CreateDirectory(BattlEyePath);
        Directory.CreateDirectory(LogPath);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                AgentStatePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var runtimeMode = UnixFileMode.UserRead
                              | UnixFileMode.UserWrite
                              | UnixFileMode.UserExecute
                              | UnixFileMode.GroupRead
                              | UnixFileMode.GroupWrite
                              | UnixFileMode.GroupExecute
                              | UnixFileMode.SetGroup;
            foreach (var path in new[]
                     {
                         RuntimeBasePath,
                         DeploymentRoot,
                         InstallPath,
                         ConfigurationPath,
                         ProfilePath,
                         BattlEyePath,
                         LogPath,
                     })
            {
                File.SetUnixFileMode(
                    path,
                    path == RuntimeBasePath ? runtimeMode | UnixFileMode.StickyBit : runtimeMode);
            }
        }
    }
}
