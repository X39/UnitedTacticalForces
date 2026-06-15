using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

public class CloudWorkerBootstrapConfigTests
{
    [Fact]
    public async Task LoadAsync_ReadsGuidScopedBootstrapContract()
    {
        using var directory = new TemporaryDirectory();
        var expected = CreateBootstrap();
        var path = System.IO.Path.Combine(directory.Path, "bootstrap.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(expected, CloudWorkerJson.Options));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var actual = await CloudWorkerBootstrapConfig.LoadAsync(path);

        Assert.Equal(expected.WorkerId, actual.WorkerId);
        Assert.Equal(expected.DeploymentId, actual.DeploymentId);
        Assert.Equal(expected.ServerId, actual.ServerId);
        Assert.Equal("lifecycle-workers", actual.WorkerApiPath);
    }

    [Fact]
    public void Validate_RejectsAbsoluteWorkerApiPath()
    {
        var config = CreateBootstrap() with { WorkerApiPath = "https://attacker.invalid/workers" };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    internal static CloudWorkerBootstrapConfig CreateBootstrap()
        => new()
        {
            ApiBaseUrl = "https://api.example.test/api/",
            WorkerId = Guid.NewGuid(),
            DeploymentId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            GameIdentifier = "arma3",
            BootstrapCredential = "bootstrap-value-not-for-logs",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        };
}
