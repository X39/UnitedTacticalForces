using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
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
        Assert.Contains($"|| /usr/bin/docker pull {image}", service, StringComparison.Ordinal);
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
        return new CloudInitWorkerBootstrapPayloadRenderer(
            configuration,
            new TestHostEnvironment { EnvironmentName = environmentName },
            Options.Create(options));
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
        => cloudInit
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Trim())
            .Where(q => q.StartsWith("content: ", StringComparison.Ordinal))
            .Select(q => q["content: ".Length..])
            .Select(q => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(q)))
            .Single(q => q.Contains("[Service]", StringComparison.Ordinal));

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
