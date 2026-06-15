using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Installs Docker on a fresh Linux VM and starts the dedicated worker container through systemd.
/// </summary>
[Singleton<CloudInitWorkerBootstrapPayloadRenderer, IWorkerBootstrapPayloadRenderer>]
public sealed class CloudInitWorkerBootstrapPayloadRenderer(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<WorkerProvisioningOptions> options) : IWorkerBootstrapPayloadRenderer
{
    private const int RuntimeGroupId = 10001;
    private readonly WorkerProvisioningOptions _options = options.Value;

    public WorkerBootstrapPayload Render(
        WorkerBootstrapConfiguration bootstrap,
        Guid serverId,
        string gameIdentifier,
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
    {
        ValidateOptions();
        var networkFailure = RuntimeInboundNetworkPlanValidator.Validate(inboundNetworkRequirements);
        if (networkFailure is not null)
            throw new InvalidOperationException(networkFailure);
        var apiBaseUrl = configuration[Constants.Configuration.General.ApiBaseUrl]?.Trim();
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri)
            || apiUri.Scheme is not ("https" or "http")
            || apiUri.Scheme == "http" && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "General:ApiBaseUrl must be an absolute HTTPS URL before a cloud worker can be provisioned.");
        }

        var workerApiPath = bootstrap.RegistrationPath.Trim('/');
        var registrationSuffix = $"/{bootstrap.WorkerId:D}/register";
        if (workerApiPath.EndsWith(registrationSuffix, StringComparison.OrdinalIgnoreCase))
            workerApiPath = workerApiPath[..^registrationSuffix.Length];
        var bootstrapDocument = JsonSerializer.Serialize(new
        {
            apiBaseUrl,
            workerId = bootstrap.WorkerId,
            deploymentId = bootstrap.DeploymentId,
            serverId,
            gameIdentifier,
            bootstrapCredential = bootstrap.BootstrapCredential,
            expiresAt = bootstrap.ExpiresAt,
            workerApiPath,
        });

        var serviceName = $"utf-cloud-worker-{bootstrap.DeploymentId:N}";
        var service = RenderSystemdService(serviceName, inboundNetworkRequirements);
        var cloudInit = $"""
                        #cloud-config
                        package_update: true
                        packages:
                          - docker.io
                        write_files:
                          - path: {_options.BootstrapPath}
                            owner: 'root:root'
                            permissions: '0400'
                            encoding: b64
                            content: {Base64(bootstrapDocument)}
                          - path: /etc/systemd/system/{serviceName}.service
                            owner: root:root
                            permissions: '0644'
                            encoding: b64
                            content: {Base64(service)}
                        runcmd:
                          - [ mkdir, -p, {_options.StateDirectory}, {_options.RuntimeDirectory} ]
                          - [ chown, -R, 'root:root', {_options.StateDirectory} ]
                          - [ chmod, '0700', {_options.StateDirectory} ]
                          - [ chown, -R, 'root:{RuntimeGroupId}', {_options.RuntimeDirectory} ]
                          - [ chmod, '2770', {_options.RuntimeDirectory} ]
                          - [ systemctl, daemon-reload ]
                          - [ systemctl, enable, --now, {serviceName}.service ]
                        """;
        return new WorkerBootstrapPayload(cloudInit);
    }

    private string RenderSystemdService(
        string serviceName,
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
    {
        var publishedPorts = string.Join(' ', inboundNetworkRequirements.Select(RenderPublishedPort));
        return $"""
           [Unit]
           Description=UTF dedicated cloud game-server worker
           Wants=network-online.target docker.service
           After=network-online.target docker.service

           [Service]
           Type=simple
           Restart=always
           RestartSec=5
           ExecStartPre=/bin/sh -c '/usr/bin/docker image inspect {_options.ContainerImage} >/dev/null 2>&1 || /usr/bin/docker pull {_options.ContainerImage}'
           ExecStartPre=-/usr/bin/docker rm -f {serviceName}
           ExecStartPre=/bin/sh -c '/usr/sbin/iptables -C DOCKER-USER -d 169.254.169.254/32 -j REJECT >/dev/null 2>&1 || /usr/sbin/iptables -I DOCKER-USER -d 169.254.169.254/32 -j REJECT'
           ExecStart=/usr/bin/docker run --rm --name {serviceName} --user 0:{RuntimeGroupId} \
             --network bridge {publishedPorts} --read-only --tmpfs /tmp:rw,noexec,nosuid,nodev,size=256m \
             --cap-drop ALL --cap-add SETUID --cap-add SETGID --security-opt no-new-privileges:true \
             --pids-limit {_options.ContainerPidsLimit} --memory {_options.ContainerMemoryLimit} \
             --log-driver json-file --log-opt max-size={_options.ContainerLogMaxSize} \
             --log-opt max-file={_options.ContainerLogMaxFiles} \
             --mount type=bind,src={_options.BootstrapPath},dst=/run/secrets/utf-worker-bootstrap.json,readonly \
             --mount type=bind,src={_options.StateDirectory},dst=/var/lib/united-tactical-forces/worker \
             --mount type=bind,src={_options.RuntimeDirectory},dst=/srv/united-tactical-forces/runtime \
             {_options.ContainerImage}
           ExecStop=/usr/bin/docker stop --time 30 {serviceName}
           TimeoutStopSec=45

           [Install]
           WantedBy=multi-user.target
           """;
    }

    private static string RenderPublishedPort(RuntimeInboundNetworkRequirement requirement)
    {
        var protocol = requirement.Protocol switch
        {
            ERuntimeNetworkProtocol.Tcp => "tcp",
            ERuntimeNetworkProtocol.Udp => "udp",
            _ => throw new InvalidOperationException("Unsupported container network protocol."),
        };
        var ports = requirement.StartPort == requirement.EndPort
            ? requirement.StartPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"{requirement.StartPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{requirement.EndPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return $"--publish {ports}:{ports}/{protocol}";
    }

    private void ValidateOptions()
    {
        if (!IsSafeContainerImage(_options.ContainerImage)
            || !environment.IsDevelopment() && !HasImmutableSha256Digest(_options.ContainerImage)
            || !IsSafeAbsolutePath(_options.BootstrapPath)
            || !IsSafeAbsolutePath(_options.StateDirectory)
            || !IsSafeAbsolutePath(_options.RuntimeDirectory)
            || _options.ContainerPidsLimit is < 64 or > 32768
            || !IsSafeSize(_options.ContainerMemoryLimit)
            || !IsSafeSize(_options.ContainerLogMaxSize)
            || _options.ContainerLogMaxFiles is < 1 or > 20)
        {
            throw new InvalidOperationException(
                "Hosting:WorkerProvisioning requires safe paths and, outside Development, an immutable "
                + "container image reference ending in @sha256:<64 hex characters>.");
        }
    }

    private static bool IsSafeContainerImage(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.All(q => char.IsAsciiLetterOrDigit(q) || q is '.' or '/' or ':' or '-' or '_' or '@');

    private static bool HasImmutableSha256Digest(string value)
    {
        const string marker = "@sha256:";
        var markerIndex = value.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= 0)
            return false;
        var digest = value[(markerIndex + marker.Length)..];
        return digest.Length == 64 && digest.All(Uri.IsHexDigit);
    }

    private static bool IsSafeAbsolutePath(string value)
        => value.StartsWith("/", StringComparison.Ordinal)
           && !value.Contains("..", StringComparison.Ordinal)
           && value.All(q => char.IsAsciiLetterOrDigit(q) || q is '/' or '.' or '-' or '_');

    private static bool IsSafeSize(string value)
    {
        if (value.Length is < 2 or > 16 || value[^1] is not ('b' or 'k' or 'm' or 'g'))
            return false;
        return decimal.TryParse(
                   value.AsSpan(0, value.Length - 1),
                   System.Globalization.NumberStyles.AllowDecimalPoint,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var magnitude)
               && magnitude > 0;
    }

    private static string Base64(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
