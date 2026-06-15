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

    public string BootstrapPath { get; set; } = "/etc/united-tactical-forces/bootstrap.json";

    public string StateDirectory { get; set; } = "/var/lib/united-tactical-forces/worker";

    public string RuntimeDirectory { get; set; } = "/srv/united-tactical-forces/runtime";

    public int ContainerPidsLimit { get; set; } = 1024;

    public string ContainerMemoryLimit { get; set; } = "7g";

    public string ContainerLogMaxSize { get; set; } = "10m";

    public int ContainerLogMaxFiles { get; set; } = 3;
}
