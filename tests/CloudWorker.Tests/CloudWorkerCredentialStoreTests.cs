using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

public class CloudWorkerCredentialStoreTests
{
    [Fact]
    public async Task SaveAsync_IsAtomicAndOwnerRestricted()
    {
        using var directory = new TemporaryDirectory();
        var store = new CloudWorkerCredentialStore(directory.Path);
        var connection = CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig();
        var first = new StoredWorkerCredential(connection, "credential-one", DateTimeOffset.UtcNow.AddHours(1), 7);
        var second = new StoredWorkerCredential(connection, "credential-two", DateTimeOffset.UtcNow.AddHours(2), 11);

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        Assert.Equal(second, await store.ReadAsync());
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(store.CredentialPath));
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsEmptyCredential()
    {
        using var directory = new TemporaryDirectory();
        var store = new CloudWorkerCredentialStore(directory.Path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
            new StoredWorkerCredential(
                CloudWorkerBootstrapConfigTests.CreateBootstrap().ToConnectionConfig(),
                "  ",
                DateTimeOffset.UtcNow.AddHours(1),
                15)));
    }
}
