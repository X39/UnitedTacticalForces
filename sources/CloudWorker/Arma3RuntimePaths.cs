using System.Globalization;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Worker-owned structural paths for one deployment. No command payload may replace them.
/// </summary>
public sealed class Arma3RuntimePaths
{
    /// <summary>The Steam application that supplies the dedicated-server executable.</summary>
    public const int DedicatedServerAppId = 233780;
    /// <summary>The Steam Workshop application that owns Arma content.</summary>
    public const int WorkshopAppId = 107410;

    /// <summary>Resolves a workload's mutable paths while sharing the deployment installation.</summary>
    /// <param name="runtimeBasePath">The mounted runtime filesystem root.</param>
    /// <param name="agentStateBasePath">The worker-owned durable state root.</param>
    /// <param name="deploymentId">The deployment that owns these paths.</param>
    /// <param name="executableName">The installed executable file name.</param>
    /// <param name="serviceIdentifier">The stable workload identifier.</param>
    /// <param name="serviceOrdinal">The workload instance ordinal.</param>
    /// <exception cref="ArgumentException">An identity or executable name cannot safely identify local paths.</exception>
    public Arma3RuntimePaths(
        string runtimeBasePath,
        string agentStateBasePath,
        Guid deploymentId,
        string executableName,
        string serviceIdentifier = "game-server",
        int serviceOrdinal = 0)
    {
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("Deployment identity cannot be empty.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(executableName)
            || executableName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Arma executable name must be a file name.", nameof(executableName));
        }
        if (string.IsNullOrWhiteSpace(serviceIdentifier)
            || serviceIdentifier.Length > 128
            || serviceIdentifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')
            || serviceOrdinal < 0)
        {
            throw new ArgumentException("Arma workload identity cannot safely identify local paths.", nameof(serviceIdentifier));
        }
        ServiceIdentifier = serviceIdentifier;
        ServiceOrdinal = serviceOrdinal;
        var primaryPaths = serviceIdentifier == "game-server" && serviceOrdinal == 0;
        var workloadDirectory = $"{serviceIdentifier}-{serviceOrdinal.ToString(CultureInfo.InvariantCulture)}";
        RuntimeBasePath = Path.GetFullPath(runtimeBasePath);
        AgentStateBasePath = Path.GetFullPath(agentStateBasePath);
        DeploymentRoot = Path.Combine(RuntimeBasePath, deploymentId.ToString("N"));
        AgentStatePath = Path.Combine(AgentStateBasePath, "deployments", deploymentId.ToString("N"));
        InstallPath = Path.Combine(DeploymentRoot, "server-instance");
        SteamAppsPath = Path.Combine(InstallPath, "steamapps");
        SteamWorkshopPath = Path.Combine(SteamAppsPath, "workshop");
        SteamWorkshopContentRootPath = Path.Combine(SteamWorkshopPath, "content");
        SteamWorkshopContentPath = Path.Combine(
            SteamWorkshopContentRootPath,
            WorkshopAppId.ToString(CultureInfo.InvariantCulture));
        ConfigurationPath = primaryPaths
            ? Path.Combine(DeploymentRoot, "server")
            : Path.Combine(DeploymentRoot, "workloads", workloadDirectory);
        ProfilePath = Path.Combine(ConfigurationPath, "profiles");
        BattlEyePath = Path.Combine(ConfigurationPath, "battleye");
        LogPath = primaryPaths
            ? Path.Combine(DeploymentRoot, "logs")
            : Path.Combine(DeploymentRoot, "logs", workloadDirectory);
        ExecutablePath = Path.Combine(InstallPath, executableName);
        ServerConfigurationPath = Path.Combine(ConfigurationPath, "server.cfg");
        BasicConfigurationPath = Path.Combine(ConfigurationPath, "basic.cfg");
        BattlEyeConfigurationPath = Path.Combine(BattlEyePath, "beserver.cfg");
        ProcessIdentityPath = Path.Combine(
            AgentStatePath, primaryPaths ? "arma3-process.json" : $"arma3-process-{workloadDirectory}.json");
        ProcessLaunchIntentPath = ProcessIdentityPath + ".launch";
        PreparationMarkerPath = Path.Combine(AgentStatePath, "arma3-prepared");
        RuntimeLogPath = Path.Combine(LogPath, "arma3-runtime.log");
        WorkshopManifestPath = Path.Combine(SteamWorkshopPath, $"appworkshop_{WorkshopAppId}.acf");
        RuntimeManifestPath = Path.Combine(SteamAppsPath, $"appmanifest_{DedicatedServerAppId}.acf");
    }

    /// <summary>Gets the stable workload identifier represented by mutable paths.</summary>
    /// <value>The assigned workload identifier.</value>
    public string ServiceIdentifier { get; }
    /// <summary>Gets the workload instance ordinal represented by mutable paths.</summary>
    /// <value>The nonnegative workload ordinal.</value>
    public int ServiceOrdinal { get; }
    /// <summary>Gets the durable marker that prevents relaunch after an uncertain spawn.</summary>
    /// <value>The worker-owned launch-intent file path.</value>
    public string ProcessLaunchIntentPath { get; }
    /// <summary>Gets the mounted root for deployment runtimes.</summary>
    /// <value>The absolute filesystem path.</value>
    public string RuntimeBasePath { get; }
    /// <summary>Gets the worker-owned state root.</summary>
    /// <value>The absolute filesystem path.</value>
    public string AgentStateBasePath { get; }
    /// <summary>Gets the runtime directory shared by this deployment.</summary>
    /// <value>The absolute filesystem path.</value>
    public string DeploymentRoot { get; }
    /// <summary>Gets the private durable state directory for this deployment.</summary>
    /// <value>The absolute filesystem path.</value>
    public string AgentStatePath { get; }
    /// <summary>Gets the shared game and mod installation directory.</summary>
    /// <value>The absolute filesystem path.</value>
    public string InstallPath { get; }
    /// <summary>Gets the shared Workshop content directory.</summary>
    /// <value>The absolute filesystem path.</value>
    public string SteamWorkshopContentPath { get; }
    /// <summary>Gets the mutable configuration directory for this workload.</summary>
    /// <value>The absolute filesystem path.</value>
    public string ConfigurationPath { get; }
    /// <summary>Gets the isolated profile directory for this workload.</summary>
    /// <value>The absolute filesystem path.</value>
    public string ProfilePath { get; }
    /// <summary>Gets the BattlEye configuration directory for this workload.</summary>
    /// <value>The absolute filesystem path.</value>
    public string BattlEyePath { get; }
    /// <summary>Gets the isolated diagnostic directory for this workload.</summary>
    /// <value>The absolute filesystem path.</value>
    public string LogPath { get; }
    /// <summary>Gets the shared installed executable path.</summary>
    /// <value>The absolute filesystem path.</value>
    public string ExecutablePath { get; }
    /// <summary>Gets the server configuration file path.</summary>
    /// <value>The absolute filesystem path.</value>
    public string ServerConfigurationPath { get; }
    /// <summary>Gets the engine configuration file path.</summary>
    /// <value>The absolute filesystem path.</value>
    public string BasicConfigurationPath { get; }
    /// <summary>Gets the BattlEye server configuration file path.</summary>
    /// <value>The absolute filesystem path.</value>
    public string BattlEyeConfigurationPath { get; }
    /// <summary>Gets the private process identity file for this workload.</summary>
    /// <value>The absolute filesystem path.</value>
    public string ProcessIdentityPath { get; }
    /// <summary>Gets the shared installation completion marker.</summary>
    /// <value>The absolute filesystem path.</value>
    public string PreparationMarkerPath { get; }
    /// <summary>Gets the runtime log file for this workload.</summary>
    /// <value>The absolute filesystem path.</value>
    public string RuntimeLogPath { get; }
    /// <summary>Gets the shared Workshop version manifest path.</summary>
    /// <value>The absolute filesystem path.</value>
    public string WorkshopManifestPath { get; }
    /// <summary>Gets the shared Steam application manifest path.</summary>
    /// <value>The absolute filesystem path.</value>
    public string RuntimeManifestPath { get; }
    /// <summary>Gets the shared Steam application metadata directory.</summary>
    /// <value>The absolute filesystem path.</value>
    private string SteamAppsPath { get; }
    /// <summary>Gets the shared Steam Workshop metadata directory.</summary>
    /// <value>The absolute filesystem path.</value>
    private string SteamWorkshopPath { get; }
    /// <summary>Gets the parent directory of Workshop application content.</summary>
    /// <value>The absolute filesystem path.</value>
    private string SteamWorkshopContentRootPath { get; }

    /// <summary>Creates runtime directories and restricts agent state to its owner.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RuntimeBasePath);
        Directory.CreateDirectory(AgentStatePath);
        Directory.CreateDirectory(DeploymentRoot);
        Directory.CreateDirectory(InstallPath);
        Directory.CreateDirectory(SteamWorkshopContentPath);
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
                         SteamAppsPath,
                         SteamWorkshopPath,
                         SteamWorkshopContentRootPath,
                         SteamWorkshopContentPath,
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

    /// <summary>Locates an item within the shared Workshop installation.</summary>
    /// <param name="workshopItemId">The positive Steam Workshop item identifier.</param>
    /// <returns>The absolute item directory.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The item identifier is zero.</exception>
    public string GetSteamWorkshopItemPath(ulong workshopItemId)
    {
        if (workshopItemId == 0)
            throw new ArgumentOutOfRangeException(nameof(workshopItemId));
        return Path.Combine(
            SteamWorkshopContentPath,
            workshopItemId.ToString(CultureInfo.InvariantCulture));
    }
}
