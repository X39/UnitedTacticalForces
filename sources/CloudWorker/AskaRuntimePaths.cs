namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Resolves deployment-bound ASKA runtime, state, save, and log paths.</summary>
public sealed class AskaRuntimePaths
{
    /// <summary>Gets the ASKA dedicated-server Steam application identifier.</summary>
    public const int ApplicationId = 3246670;

    /// <summary>Initializes ASKA paths for one deployment.</summary>
    /// <param name="runtimeRoot">Worker runtime root.</param>
    /// <param name="stateRoot">Worker-private agent state root.</param>
    /// <param name="deploymentId">Deployment identifier.</param>
    /// <param name="executableRelativePath">ASKA executable relative to the install directory.</param>
    public AskaRuntimePaths(string runtimeRoot, string stateRoot, Guid deploymentId, string executableRelativePath)
    {
        DeploymentRoot = Path.Combine(Path.GetFullPath(runtimeRoot), deploymentId.ToString("N"));
        AgentStateRoot = Path.Combine(Path.GetFullPath(stateRoot), "deployments", deploymentId.ToString("N"));
        InstallPath = Path.Combine(DeploymentRoot, "server-instance");
        ExecutablePath = Path.GetFullPath(Path.Combine(InstallPath, executableRelativePath));
        if (!ExecutablePath.StartsWith(Path.GetFullPath(InstallPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The ASKA executable leaves its install root.");
        WinePrefixPath = Path.Combine(DeploymentRoot, "wine-prefix");
        RuntimeConfigurationRoot = Path.Combine(DeploymentRoot, ".utf-runtime", "aska");
        PropertiesPath = Path.Combine(RuntimeConfigurationRoot, "server properties.txt");
        PersistentStateRoot = Path.Combine(DeploymentRoot, "aska-state");
        SaveIdPath = Path.Combine(PersistentStateRoot, "save-id.txt");
        LogPath = Path.Combine(DeploymentRoot, "logs");
        RuntimeLogPath = Path.Combine(LogPath, "aska-server.log");
        ProcessIdentityPath = Path.Combine(AgentStateRoot, "aska-process.json");
        PreparationMarkerPath = Path.Combine(AgentStateRoot, "aska-prepared");
        RuntimeManifestPath = Path.Combine(InstallPath, "steamapps", $"appmanifest_{ApplicationId}.acf");
        SaveRoot = Path.Combine(WinePrefixPath, "drive_c", "users", "utfgame", "AppData", "LocalLow",
            "Sand Sailor Studio", "Aska", "data", "server");
    }

    /// <summary>Gets the deployment-visible runtime root.</summary>
    public string DeploymentRoot { get; }
    /// <summary>Gets the worker-private state root.</summary>
    public string AgentStateRoot { get; }
    /// <summary>Gets the Steam installation directory.</summary>
    public string InstallPath { get; }
    /// <summary>Gets the ASKA executable path.</summary>
    public string ExecutablePath { get; }
    /// <summary>Gets the Wine prefix directory.</summary>
    public string WinePrefixPath { get; }
    /// <summary>Gets the non-preserved runtime configuration directory.</summary>
    public string RuntimeConfigurationRoot { get; }
    /// <summary>Gets the token-bearing runtime properties file.</summary>
    public string PropertiesPath { get; }
    /// <summary>Gets the preservation-safe state directory.</summary>
    public string PersistentStateRoot { get; }
    /// <summary>Gets the sanitized save identifier file.</summary>
    public string SaveIdPath { get; }
    /// <summary>Gets the ASKA log directory.</summary>
    public string LogPath { get; }
    /// <summary>Gets the active ASKA runtime log.</summary>
    public string RuntimeLogPath { get; }
    /// <summary>Gets the persisted process identity file.</summary>
    public string ProcessIdentityPath { get; }
    /// <summary>Gets the successful-installation marker.</summary>
    public string PreparationMarkerPath { get; }
    /// <summary>Gets the Steam runtime manifest path.</summary>
    public string RuntimeManifestPath { get; }
    /// <summary>Gets the ASKA server-save directory within the Wine prefix.</summary>
    public string SaveRoot { get; }

    /// <summary>Creates required directories with worker/game shared permissions.</summary>
    public void EnsureDirectories()
    {
        foreach (var path in new[]
                 {
                     DeploymentRoot, AgentStateRoot, InstallPath, WinePrefixPath, RuntimeConfigurationRoot,
                     PersistentStateRoot, LogPath,
                 })
            Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
            return;
        File.SetUnixFileMode(AgentStateRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                     | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                     | UnixFileMode.SetGroup;
        foreach (var path in new[]
                 {
                     DeploymentRoot, InstallPath, WinePrefixPath, RuntimeConfigurationRoot, PersistentStateRoot, LogPath,
                 })
            File.SetUnixFileMode(path, shared);
    }
}
