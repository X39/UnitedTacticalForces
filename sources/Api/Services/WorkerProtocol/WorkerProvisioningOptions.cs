namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Installation-specific worker image and cloud-init settings.
/// </summary>
public sealed class WorkerProvisioningOptions
{
    /// <summary>Identifies the server-only configuration section used for cloud worker provisioning.</summary>
    public const string SectionName = "Hosting:WorkerProvisioning";

    /// <summary>
    /// Gets or sets the shared worker image containing the runtime support for every registered game executor.
    /// Development may use a mutable local tag; other environments require an immutable SHA-256 digest.
    /// </summary>
    /// <value>The common worker reference used for Arma 3, ASKA, and Counter-Strike 2 deployments.</value>
    public string ContainerImage { get; set; } = "ghcr.io/x39/united-tactical-forces-cloud-worker:development";

    /// <summary>
    /// Registry authority used for the worker-image pull credentials.
    /// </summary>
    /// <value>The registry host matching the configured shared image reference.</value>
    public string RegistryServer { get; set; } = "ghcr.io";

    /// <summary>
    /// Registry user paired with <see cref="RegistryToken"/>. This value remains server-side.
    /// </summary>
    /// <value>The dedicated read-only pull principal, or an empty value for anonymous development pulls.</value>
    public string RegistryUsername { get; set; } = "";

    /// <summary>
    /// Read-only registry token used only by the provisioned host to pull the worker image.
    /// </summary>
    /// <value>The private pull credential, never a frontend or logging value.</value>
    public string RegistryToken { get; set; } = "";

    /// <summary>
    /// Fails provisioning when registry credentials are absent.
    /// </summary>
    /// <value>Whether the selected registry requires a complete username and token pair.</value>
    public bool RegistryAuthenticationRequired { get; set; }

    /// <summary>Gets or sets the root-only Docker authentication file on the deployment host.</summary>
    /// <value>An absolute host path kept outside the game runtime mount.</value>
    public string RegistryConfigPath { get; set; } = "/etc/united-tactical-forces/registry/config.json";

    /// <summary>Gets or sets the deployment-scoped credential document installed by cloud-init.</summary>
    /// <value>An absolute host path mounted read-only into the dedicated worker.</value>
    public string BootstrapPath { get; set; } = "/etc/united-tactical-forces/bootstrap.json";

    /// <summary>Gets or sets private durable worker credentials and command journal storage.</summary>
    /// <value>An absolute host directory excluded from game-process access.</value>
    public string StateDirectory { get; set; } = "/var/lib/united-tactical-forces/worker";

    /// <summary>Gets or sets the separate installation and saved-game storage mount.</summary>
    /// <value>An absolute deployment directory shared only with the worker's isolated game identities.</value>
    public string RuntimeDirectory { get; set; } = "/srv/united-tactical-forces/runtime";

    /// <summary>Gets or sets the deployment worker container's process and thread bound.</summary>
    /// <value>The PID controller limit validated before rendering the container command.</value>
    public int ContainerPidsLimit { get; set; } = 1024;

    /// <summary>Gets or sets the memory bound shared by the agent and selected game process.</summary>
    /// <value>A validated Docker byte-size expression, such as <c>7g</c>.</value>
    public string ContainerMemoryLimit { get; set; } = "7g";

    /// <summary>Gets or sets the maximum size of each rotated container log file.</summary>
    /// <value>A validated Docker byte-size expression.</value>
    public string ContainerLogMaxSize { get; set; } = "10m";

    /// <summary>Gets or sets the number of rotated container logs retained on a deployment host.</summary>
    /// <value>A bounded positive count validated before provisioning.</value>
    public int ContainerLogMaxFiles { get; set; } = 3;

    /// <summary>
    /// Enables key-only emergency SSH access on newly provisioned deployment hosts.
    /// </summary>
    /// <value>Whether the explicitly configured administrator and source networks are installed.</value>
    public bool AdministrativeSshEnabled { get; set; }

    /// <summary>Gets or sets the host account used for authorized emergency access.</summary>
    /// <value>A validated non-root Linux account name.</value>
    public string AdministrativeSshUser { get; set; } = "utf-admin";

    /// <summary>
    /// OpenSSH public keys installed for the emergency administrator account. Private keys remain operator-owned.
    /// </summary>
    /// <value>Approved public keys; no private-key material is accepted here.</value>
    public string[] AdministrativeSshAuthorizedKeys { get; set; } = [];

    /// <summary>
    /// Provider-firewall source networks allowed to reach TCP/22. Required when SSH access is enabled.
    /// </summary>
    /// <value>The explicit operator CIDR allowlist for newly provisioned hosts.</value>
    public string[] AdministrativeSshSourceCidrs { get; set; } = [];
}
