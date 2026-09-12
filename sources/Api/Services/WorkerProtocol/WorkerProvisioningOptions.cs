namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Installation-specific worker image and cloud-init settings.
/// </summary>
public sealed class WorkerProvisioningOptions
{
    public const string SectionName = "Hosting:WorkerProvisioning";

    /// <summary>
    /// Development may use a mutable local tag. Non-development provisioning requires an immutable sha256 digest.
    /// </summary>
    public string ContainerImage { get; set; } = "ghcr.io/x39/united-tactical-forces-cloud-worker:development";

    /// <summary>
    /// Gets or sets dedicated worker images keyed by the game definition's worker-image identifier.
    /// Production values must use immutable SHA-256 digests.
    /// </summary>
    public Dictionary<string, string> GameContainerImages { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registry authority used for the worker-image pull credentials.
    /// </summary>
    public string RegistryServer { get; set; } = "ghcr.io";

    /// <summary>
    /// Registry user paired with <see cref="RegistryToken"/>. This value remains server-side.
    /// </summary>
    public string RegistryUsername { get; set; } = "";

    /// <summary>
    /// Read-only registry token used only by the provisioned host to pull the worker image.
    /// </summary>
    public string RegistryToken { get; set; } = "";

    /// <summary>
    /// Fails provisioning when registry credentials are absent.
    /// </summary>
    public bool RegistryAuthenticationRequired { get; set; }

    public string RegistryConfigPath { get; set; } = "/etc/united-tactical-forces/registry/config.json";

    public string BootstrapPath { get; set; } = "/etc/united-tactical-forces/bootstrap.json";

    public string StateDirectory { get; set; } = "/var/lib/united-tactical-forces/worker";

    public string RuntimeDirectory { get; set; } = "/srv/united-tactical-forces/runtime";

    public int ContainerPidsLimit { get; set; } = 1024;

    public string ContainerMemoryLimit { get; set; } = "7g";

    public string ContainerLogMaxSize { get; set; } = "10m";

    public int ContainerLogMaxFiles { get; set; } = 3;

    /// <summary>
    /// Enables key-only emergency SSH access on newly provisioned deployment hosts.
    /// </summary>
    public bool AdministrativeSshEnabled { get; set; }

    public string AdministrativeSshUser { get; set; } = "utf-admin";

    /// <summary>
    /// OpenSSH public keys installed for the emergency administrator account. Private keys remain operator-owned.
    /// </summary>
    public string[] AdministrativeSshAuthorizedKeys { get; set; } = [];

    /// <summary>
    /// Provider-firewall source networks allowed to reach TCP/22. Required when SSH access is enabled.
    /// </summary>
    public string[] AdministrativeSshSourceCidrs { get; set; } = [];
}
