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
    IOptions<WorkerProvisioningOptions> options,
    AdministrativeSshAccess administrativeSshAccess) : IWorkerBootstrapPayloadRenderer
{
    internal const string ExternalBlockDeviceToken = "__UTF_EXTERNAL_BLOCK_DEVICE__";
    private const int RuntimeGroupId = 10001;
    private const string SteamPasswordPath = "/etc/united-tactical-forces/steam-password";
    private const string ContainerSteamPasswordPath = "/run/secrets/utf-steam-password";
    private readonly WorkerProvisioningOptions _options = options.Value;

    public WorkerBootstrapPayload Render(
        WorkerBootstrapConfiguration bootstrap,
        Guid serverId,
        string gameIdentifier,
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
        => Render(
            bootstrap,
            serverId,
            gameIdentifier,
            inboundNetworkRequirements,
            new RuntimeStoragePlan(
                X39.UnitedTacticalForces.Hosting.Contracts.ERuntimeStorageStrategy.IncludedDisk,
                0,
                0,
                _options.RuntimeDirectory,
                0));

    public WorkerBootstrapPayload Render(
        WorkerBootstrapConfiguration bootstrap,
        Guid serverId,
        string gameIdentifier,
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements,
        RuntimeStoragePlan storagePlan)
    {
        ValidateOptions();
        var steamCredentials = ResolveSteamCredentials(gameIdentifier);
        if (!IsSafeAbsolutePath(storagePlan.DeploymentRoot)
            || storagePlan.RequiredCapacityBytes < 0
            || storagePlan.SelectedCapacityBytes < storagePlan.RequiredCapacityBytes)
        {
            throw new InvalidOperationException("The resolved runtime-storage plan is invalid for bootstrap.");
        }
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
        var service = RenderSystemdService(
            serviceName,
            inboundNetworkRequirements,
            storagePlan.DeploymentRoot,
            steamCredentials);
        var storageService = RenderStorageService(storagePlan);
        var storageScript = RenderStorageScript(storagePlan, bootstrap.DeploymentId);
        var registryConfig = RenderRegistryConfig();
        var registryConfigDirectory = GetAbsoluteParentPath(_options.RegistryConfigPath);
        var administrativeSsh = administrativeSshAccess.RenderCloudInitFragment();
        var cloudInit = $"""
                        #cloud-config
                        {administrativeSsh}
                        package_update: true
                        packages:
                          - docker.io
                          - e2fsprogs
                          - util-linux
                          - openssh-server
                        write_files:
                          - path: {_options.BootstrapPath}
                            owner: 'root:root'
                            permissions: '0400'
                            encoding: b64
                            content: {Base64(bootstrapDocument)}
                          - path: {_options.RegistryConfigPath}
                            owner: 'root:root'
                            permissions: '0400'
                            encoding: b64
                            content: {Base64(registryConfig)}
                        {RenderSteamPasswordFile(steamCredentials)}
                          - path: /etc/systemd/system/{serviceName}.service
                            owner: root:root
                            permissions: '0644'
                            encoding: b64
                            content: {Base64(service)}
                          - path: /usr/local/sbin/utf-prepare-runtime-storage
                            owner: root:root
                            permissions: '0700'
                            encoding: b64
                            content: {Base64(storageScript)}
                          - path: /etc/systemd/system/utf-runtime-storage.service
                            owner: root:root
                            permissions: '0644'
                            encoding: b64
                            content: {Base64(storageService)}
                        runcmd:
                          - [ mkdir, -p, {registryConfigDirectory}, {_options.StateDirectory}, {storagePlan.DeploymentRoot} ]
                          - [ chown, 'root:root', {registryConfigDirectory}, {_options.RegistryConfigPath} ]
                          - [ chmod, '0700', {registryConfigDirectory} ]
                          - [ chmod, '0400', {_options.RegistryConfigPath} ]
                          - [ chown, -R, 'root:root', {_options.StateDirectory} ]
                          - [ chmod, '0700', {_options.StateDirectory} ]
                          - [ chown, -R, 'root:{RuntimeGroupId}', {storagePlan.DeploymentRoot} ]
                          - [ chmod, '2770', {storagePlan.DeploymentRoot} ]
                          - [ systemctl, daemon-reload ]
                          - [ systemctl, enable, --now, utf-runtime-storage.service ]
                          - [ systemctl, enable, --now, {serviceName}.service ]
                        """;
        return new WorkerBootstrapPayload(cloudInit);
    }

    private string RenderRegistryConfig()
    {
        var auths = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(_options.RegistryUsername) && !string.IsNullOrEmpty(_options.RegistryToken))
        {
            var credential = Base64($"{_options.RegistryUsername}:{_options.RegistryToken}");
            auths[_options.RegistryServer] = new { auth = credential };
        }
        return JsonSerializer.Serialize(new { auths });
    }

    private (string Username, string Password)? ResolveSteamCredentials(string gameIdentifier)
    {
        if (!string.Equals(gameIdentifier, "arma3", StringComparison.OrdinalIgnoreCase))
            return null;

        var username = configuration["Steam:Username"];
        var password = configuration["Steam:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Steam:Username and Steam:Password must be configured before an Arma 3 worker can be provisioned.");
        }
        if (username.Length > 128
            || username.Any(q => !char.IsAsciiLetterOrDigit(q) && q is not ('_' or '-' or '.' or '@'))
            || password.Length > 4096
            || password.Any(char.IsControl))
        {
            throw new InvalidOperationException("The configured Steam worker credentials are invalid.");
        }

        return (username, password);
    }

    private static string RenderSteamPasswordFile((string Username, string Password)? credentials)
        => credentials is null
            ? string.Empty
            : $"""
                - path: {SteamPasswordPath}
                  owner: 'root:root'
                  permissions: '0400'
                  encoding: b64
                  content: {Base64(credentials.Value.Password)}
              """;

    private string RenderStorageService(RuntimeStoragePlan storagePlan)
        => $"""
           [Unit]
           Description=Verify UTF deployment storage
           Wants=local-fs.target
           After=local-fs.target
           Before=utf-cloud-worker.target

           [Service]
           Type=oneshot
           ExecStart=/usr/local/sbin/utf-prepare-runtime-storage
           RemainAfterExit=yes

           [Install]
           WantedBy=multi-user.target
           """;

    private string RenderStorageScript(RuntimeStoragePlan storagePlan, Guid deploymentId)
    {
        var requiredBytes = storagePlan.RequiredCapacityBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!storagePlan.UsesExternalStorage)
        {
            return $"""
                   #!/bin/sh
                   set -eu
                   mount_path='{storagePlan.DeploymentRoot}'
                   mkdir -p "$mount_path"
                   chown root:{RuntimeGroupId} "$mount_path"
                   chmod 2770 "$mount_path"
                   available="$(df -PB1 "$mount_path" | tail -n 1 | tr -s ' ' | cut -d ' ' -f 4)"
                   test "$available" -ge '{requiredBytes}'
                   """;
        }

        var expectedBytes = storagePlan.SelectedCapacityBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var fileSystemLabel = $"UTF_{deploymentId:N}"[..16];
        return $"""
               #!/bin/sh
               set -eu
               device='{ExternalBlockDeviceToken}'
               mount_path='{storagePlan.DeploymentRoot}'
               expected_label='{fileSystemLabel}'
               test -b "$device"
               actual_bytes="$(blockdev --getsize64 "$device")"
               test "$actual_bytes" -ge '{expectedBytes}'
               if ! blkid -p "$device" >/dev/null 2>&1; then
                 mkfs.ext4 -F -L "$expected_label" "$device"
               fi
               test "$(blkid -s TYPE -o value "$device")" = 'ext4'
               test "$(blkid -s LABEL -o value "$device")" = "$expected_label"
               mkdir -p "$mount_path"
               fstab_line="LABEL=$expected_label $mount_path ext4 defaults,nofail,x-systemd.device-timeout=60 0 2"
               grep -Fqx "$fstab_line" /etc/fstab || printf '%s\n' "$fstab_line" >> /etc/fstab
               mount "$mount_path"
               test "$(findmnt -n -o FSTYPE --target "$mount_path")" = 'ext4'
               mounted_source="$(readlink -f "$(findmnt -n -o SOURCE --target "$mount_path")")"
               test "$mounted_source" = "$(readlink -f "$device")"
               available="$(df -PB1 "$mount_path" | tail -n 1 | tr -s ' ' | cut -d ' ' -f 4)"
               test "$available" -ge '{requiredBytes}'
               chown root:{RuntimeGroupId} "$mount_path"
               chmod 2770 "$mount_path"
               """;
    }

    private string RenderSystemdService(
        string serviceName,
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements,
        string deploymentRoot,
        (string Username, string Password)? steamCredentials)
    {
        var publishedPorts = string.Join(' ', inboundNetworkRequirements.Select(RenderPublishedPort));
        var steamArguments = steamCredentials is null
            ? string.Empty
            : $"""
              --env UTF_STEAM_USERNAME={steamCredentials.Value.Username} \
                --env UTF_STEAM_PASSWORD_FILE={ContainerSteamPasswordPath} \
                --mount type=bind,src={SteamPasswordPath},dst={ContainerSteamPasswordPath},readonly \
              """;
        return $"""
           [Unit]
           Description=UTF dedicated cloud game-server worker
           Wants=network-online.target docker.service
           Requires=utf-runtime-storage.service
           After=network-online.target docker.service utf-runtime-storage.service

           [Service]
           Type=simple
           Restart=always
           RestartSec=5
           ExecStartPre=/bin/sh -c '/usr/bin/docker image inspect {_options.ContainerImage} >/dev/null 2>&1 || /usr/bin/docker --config {GetAbsoluteParentPath(_options.RegistryConfigPath)} pull {_options.ContainerImage}'
           ExecStartPre=-/usr/bin/docker rm -f {serviceName}
           ExecStartPre=/bin/sh -c '/usr/sbin/iptables -C DOCKER-USER -d 169.254.169.254/32 -j REJECT >/dev/null 2>&1 || /usr/sbin/iptables -I DOCKER-USER -d 169.254.169.254/32 -j REJECT'
           ExecStart=/usr/bin/docker run --rm --name {serviceName} --user 0:{RuntimeGroupId} \
             --network bridge {publishedPorts} --read-only --tmpfs /tmp:rw,noexec,nosuid,nodev,size=256m \
             --cap-drop ALL --cap-add SETUID --cap-add SETGID --cap-add CHOWN --cap-add FOWNER \
             --security-opt no-new-privileges:true \
             --pids-limit {_options.ContainerPidsLimit} --memory {_options.ContainerMemoryLimit} \
             --log-driver json-file --log-opt max-size={_options.ContainerLogMaxSize} \
             --log-opt max-file={_options.ContainerLogMaxFiles} \
             --mount type=bind,src={_options.BootstrapPath},dst=/run/secrets/utf-worker-bootstrap.json,readonly \
             --mount type=bind,src={_options.StateDirectory},dst=/var/lib/united-tactical-forces/worker \
             --mount type=bind,src={deploymentRoot},dst=/srv/united-tactical-forces/runtime \
             {steamArguments}
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
            || !IsSafeRegistryServer(_options.RegistryServer)
            || !IsSafeRegistryCredentialPair()
            || !IsSafeAbsolutePath(_options.RegistryConfigPath)
            || _options.RegistryConfigPath.LastIndexOf('/') <= 0
            || !IsSafeAbsolutePath(_options.StateDirectory)
            || !IsSafeAbsolutePath(_options.RuntimeDirectory)
            || _options.ContainerPidsLimit is < 64 or > 32768
            || !IsSafeSize(_options.ContainerMemoryLimit)
            || !IsSafeSize(_options.ContainerLogMaxSize)
            || _options.ContainerLogMaxFiles is < 1 or > 20)
        {
            throw new InvalidOperationException(
                "Hosting:WorkerProvisioning requires safe paths, valid paired registry credentials when required, "
                + "and, outside Development, an immutable container image reference ending in "
                + "@sha256:<64 hex characters>.");
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

    private static string GetAbsoluteParentPath(string value)
    {
        var separatorIndex = value.LastIndexOf('/');
        if (separatorIndex <= 0)
            throw new InvalidOperationException("The registry config path has no absolute parent directory.");
        return value[..separatorIndex];
    }

    private static bool IsSafeRegistryServer(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length <= 253
           && value.All(q => char.IsAsciiLetterOrDigit(q) || q is '.' or '-' or ':');

    private bool IsSafeRegistryCredentialPair()
    {
        var hasUsername = !string.IsNullOrEmpty(_options.RegistryUsername);
        var hasToken = !string.IsNullOrEmpty(_options.RegistryToken);
        if (hasUsername != hasToken || _options.RegistryAuthenticationRequired && !hasUsername)
            return false;
        return !hasUsername
               || _options.RegistryUsername.Length <= 256
               && _options.RegistryToken.Length <= 4096
               && !_options.RegistryUsername.Any(char.IsControl)
               && !_options.RegistryToken.Any(char.IsControl);
    }

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
