using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class CloudInitWorkerBootstrapPayloadRendererTests
{
    [Fact]
    public void Production_RejectsMutableWorkerImageTag()
    {
        var renderer = CreateRenderer(
            Environments.Production,
            "ghcr.io/x39/united-tactical-forces-cloud-worker:latest");

        var exception = Assert.Throws<InvalidOperationException>(() => renderer.Render(
            Bootstrap(),
            Guid.NewGuid(),
            "arma3",
            NetworkPlan()));

        Assert.Contains("immutable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_AcceptsDigestAndDoesNotUnconditionallyPullOnRestart()
    {
        var digest = new string('a', 64);
        var image = $"ghcr.io/x39/united-tactical-forces-cloud-worker@sha256:{digest}";
        var renderer = CreateRenderer(Environments.Production, image);

        var payload = renderer.Render(Bootstrap(), Guid.NewGuid(), "arma3", NetworkPlan());

        var service = DecodeSystemdService(payload.CloudInit);
        Assert.Contains($"docker image inspect {image}", service, StringComparison.Ordinal);
        Assert.Contains(
            $"|| /usr/bin/docker --config /etc/united-tactical-forces/registry pull {image}",
            service,
            StringComparison.Ordinal);
        Assert.Contains("--network bridge", service, StringComparison.Ordinal);
        Assert.Contains("--publish 2302-2306:2302-2306/udp", service, StringComparison.Ordinal);
        Assert.DoesNotContain("--network host", service, StringComparison.Ordinal);
        Assert.Contains("--cap-drop ALL", service, StringComparison.Ordinal);
        Assert.Contains("--cap-add SETUID --cap-add SETGID", service, StringComparison.Ordinal);
        Assert.Contains("--security-opt no-new-privileges:true", service, StringComparison.Ordinal);
        Assert.Contains("--pids-limit 1024 --memory 7g", service, StringComparison.Ordinal);
        Assert.Contains("--log-opt max-size=10m", service, StringComparison.Ordinal);
        Assert.Contains("169.254.169.254/32", service, StringComparison.Ordinal);
        Assert.Contains("[ chown, -R, 'root:root', /var/lib/united-tactical-forces/worker ]", payload.CloudInit);
        Assert.Contains("[ chmod, '0700', /var/lib/united-tactical-forces/worker ]", payload.CloudInit);
    }

    [Fact]
    public void Production_PrivateRegistryRendersRootOnlyDockerAuthWithoutExposingRawToken()
    {
        var digest = new string('b', 64);
        var image = $"ghcr.io/x39/united-tactical-forces-cloud-worker@sha256:{digest}";
        const string registryToken = "github_pat_private_pull_marker";
        var renderer = CreateRenderer(
            Environments.Production,
            image,
            options =>
            {
                options.RegistryAuthenticationRequired = true;
                options.RegistryUsername = "X39";
                options.RegistryToken = registryToken;
            });

        var payload = renderer.Render(Bootstrap(), Guid.NewGuid(), "arma3", NetworkPlan());

        Assert.DoesNotContain(registryToken, payload.CloudInit, StringComparison.Ordinal);
        Assert.Contains("permissions: '0400'", payload.CloudInit, StringComparison.Ordinal);
        var files = DecodeWrittenFiles(payload.CloudInit);
        var registryConfig = files.Single(q => q.Contains("\"auths\"", StringComparison.Ordinal));
        using var document = System.Text.Json.JsonDocument.Parse(registryConfig);
        var encodedCredential = document.RootElement
            .GetProperty("auths")
            .GetProperty("ghcr.io")
            .GetProperty("auth")
            .GetString();
        Assert.NotNull(encodedCredential);
        var credential = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredential));
        Assert.Equal($"X39:{registryToken}", credential);
        var service = DecodeSystemdService(payload.CloudInit);
        Assert.Contains(
            $"docker --config /etc/united-tactical-forces/registry pull {image}",
            service,
            StringComparison.Ordinal);
        Assert.DoesNotContain(registryToken, service, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_RejectsMissingRequiredRegistryCredentials()
    {
        var digest = new string('c', 64);
        var renderer = CreateRenderer(
            Environments.Production,
            $"ghcr.io/x39/united-tactical-forces-cloud-worker@sha256:{digest}",
            options => options.RegistryAuthenticationRequired = true);

        var exception = Assert.Throws<InvalidOperationException>(() => renderer.Render(
            Bootstrap(),
            Guid.NewGuid(),
            "arma3",
            NetworkPlan()));

        Assert.Contains("registry credentials", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Development_AllowsLocalMutableImageTag()
    {
        var renderer = CreateRenderer(Environments.Development, "utf-cloud-worker:development");

        var payload = renderer.Render(Bootstrap(), Guid.NewGuid(), "arma3", NetworkPlan());

        Assert.Contains(
            "utf-cloud-worker:development",
            DecodeSystemdService(payload.CloudInit),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Provisioning_RejectsInvalidDockerSizeValues()
    {
        var renderer = CreateRenderer(
            Environments.Development,
            "utf-cloud-worker:development",
            options => options.ContainerMemoryLimit = "7garbage");

        Assert.Throws<InvalidOperationException>(() => renderer.Render(
            Bootstrap(),
            Guid.NewGuid(),
            "arma3",
            NetworkPlan()));
    }

    [Fact]
    public void AdministrativeSsh_RendersKeyOnlyAccountWithoutPublishingSshIntoWorkerContainer()
    {
        var renderer = CreateRenderer(
            Environments.Development,
            "utf-cloud-worker:development",
            options =>
            {
                options.AdministrativeSshEnabled = true;
                options.AdministrativeSshAuthorizedKeys =
                [
                    "ssh-ed25519 MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDE= utf-admin",
                ];
                options.AdministrativeSshSourceCidrs = ["203.0.113.10/32"];
            });

        var payload = renderer.Render(Bootstrap(), Guid.NewGuid(), "arma3", NetworkPlan());

        Assert.Contains("name: utf-admin", payload.CloudInit, StringComparison.Ordinal);
        Assert.Contains("ssh_pwauth: false", payload.CloudInit, StringComparison.Ordinal);
        Assert.Contains("disable_root: true", payload.CloudInit, StringComparison.Ordinal);
        Assert.Contains("ssh-ed25519 MDEyMz", payload.CloudInit, StringComparison.Ordinal);
        Assert.DoesNotContain("--publish 22:22", DecodeSystemdService(payload.CloudInit), StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrativeSsh_AddsSourceRestrictedHostFirewallRequirement()
    {
        var options = Options.Create(new WorkerProvisioningOptions
        {
            AdministrativeSshEnabled = true,
            AdministrativeSshAuthorizedKeys =
            [
                "ssh-ed25519 MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDE= utf-admin",
            ],
            AdministrativeSshSourceCidrs = ["203.0.113.10/32", "2001:db8::/64"],
        });

        var requirement = new AdministrativeSshAccess(options).AddTo(NetworkPlan()).Single(q => q.StartPort == 22);

        Assert.Equal(ERuntimeNetworkProtocol.Tcp, requirement.Protocol);
        Assert.Equal(["203.0.113.10/32", "2001:db8::/64"], requirement.SourceCidrs);
    }

    [Fact]
    public void ExternalStorage_FormatsOnlyUninitializedDeviceAndFailsClosedBeforeWorkerStarts()
    {
        var renderer = CreateRenderer(Environments.Development, "utf-cloud-worker:development");
        var deploymentId = Guid.NewGuid();
        var bootstrap = Bootstrap() with { DeploymentId = deploymentId };
        var plan = new RuntimeStoragePlan(
            ERuntimeStorageStrategy.AutomaticExternal,
            18L * RuntimeStoragePlanResolver.GiB,
            20L * RuntimeStoragePlanResolver.GiB,
            "/srv/utf/deployment",
            0.02m);

        var payload = renderer.Render(bootstrap, Guid.NewGuid(), "arma3", NetworkPlan(), plan);
        var files = DecodeWrittenFiles(payload.CloudInit);
        var script = files.Single(q => q.Contains("mkfs.ext4", StringComparison.Ordinal));
        var service = files.Single(q => q.Contains("[Service]", StringComparison.Ordinal)
                                               && q.Contains("docker run", StringComparison.Ordinal));

        Assert.Contains("test -b \"$device\"", script, StringComparison.Ordinal);
        Assert.Contains("if ! blkid -p", script, StringComparison.Ordinal);
        Assert.Contains("test \"$(blkid -s LABEL", script, StringComparison.Ordinal);
        Assert.Contains("findmnt -n -o SOURCE", script, StringComparison.Ordinal);
        Assert.Contains("blockdev --getsize64", script, StringComparison.Ordinal);
        Assert.Contains(CloudInitWorkerBootstrapPayloadRenderer.ExternalBlockDeviceToken, script, StringComparison.Ordinal);
        Assert.Contains("Requires=utf-runtime-storage.service", service, StringComparison.Ordinal);
        Assert.Contains("After=network-online.target docker.service utf-runtime-storage.service", service, StringComparison.Ordinal);
        Assert.Contains("src=/srv/utf/deployment,dst=/srv/united-tactical-forces/runtime", service, StringComparison.Ordinal);
        Assert.DoesNotContain("bootstrap-secret", script, StringComparison.Ordinal);
    }

    private static CloudInitWorkerBootstrapPayloadRenderer CreateRenderer(
        string environmentName,
        string image,
        Action<WorkerProvisioningOptions>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["General:ApiBaseUrl"] = "https://control-plane.example/api/",
            })
            .Build();
        var options = new WorkerProvisioningOptions { ContainerImage = image };
        configure?.Invoke(options);
        var wrappedOptions = Options.Create(options);
        return new CloudInitWorkerBootstrapPayloadRenderer(
            configuration,
            new TestHostEnvironment { EnvironmentName = environmentName },
            wrappedOptions,
            new AdministrativeSshAccess(wrappedOptions));
    }

    private static WorkerBootstrapConfiguration Bootstrap()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "bootstrap-secret",
            DateTimeOffset.UtcNow.AddMinutes(30),
            $"/lifecycle-workers/{Guid.NewGuid():D}/register");

    private static IReadOnlyList<RuntimeInboundNetworkRequirement> NetworkPlan()
        =>
        [
            new(ERuntimeNetworkProtocol.Udp, 2302, 2306, "Test game traffic"),
        ];

    private static string DecodeSystemdService(string cloudInit)
        => DecodeWrittenFiles(cloudInit)
            .Single(q => q.Contains("[Service]", StringComparison.Ordinal)
                         && q.Contains("docker run", StringComparison.Ordinal));

    private static IReadOnlyList<string> DecodeWrittenFiles(string cloudInit)
        => cloudInit
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Trim())
            .Where(q => q.StartsWith("content: ", StringComparison.Ordinal))
            .Select(q => q["content: ".Length..])
            .Select(q => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(q)))
            .ToArray();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
