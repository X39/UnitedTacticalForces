using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class FileSystemServerFileStorageTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        $"utf-file-storage-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task RecursiveDelete_RefusesGenericReadOnlyDescendant()
    {
        var serverId = Guid.NewGuid();
        var storage = CreateStorage();
        await storage.CreateDirectoryAsync(serverId, null, EFileContext.Persistent, "editable");
        var serverRoot = Path.Combine(_temporaryRoot, "persistent", serverId.ToString("N"));
        Directory.CreateDirectory(Path.Combine(serverRoot, "editable", "logs"));
        await File.WriteAllTextAsync(Path.Combine(serverRoot, "editable", "logs", "server.rpt"), "diagnostic");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.DeleteAsync(
            serverId,
            null,
            EFileContext.Persistent,
            "editable",
            true));

        Assert.True(File.Exists(Path.Combine(serverRoot, "editable", "logs", "server.rpt")));
    }

    [Fact]
    public async Task RecursiveDelete_RefusesPlatformMetadataDescendant()
    {
        var serverId = Guid.NewGuid();
        var storage = CreateStorage();
        await storage.CreateDirectoryAsync(serverId, null, EFileContext.Persistent, "editable");
        var serverRoot = Path.Combine(_temporaryRoot, "persistent", serverId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(serverRoot, "editable", ".utf-internal"), "metadata");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.DeleteAsync(
            serverId,
            null,
            EFileContext.Persistent,
            "editable",
            true));
    }

    [Fact]
    public async Task RecursiveDelete_RemovesWritableTree()
    {
        var serverId = Guid.NewGuid();
        var storage = CreateStorage();
        await storage.CreateDirectoryAsync(serverId, null, EFileContext.Persistent, "editable/content");
        var serverRoot = Path.Combine(_temporaryRoot, "persistent", serverId.ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(serverRoot, "editable", "content", "state.bin"), "state");

        await storage.DeleteAsync(serverId, null, EFileContext.Persistent, "editable", true);

        Assert.False(Directory.Exists(Path.Combine(serverRoot, "editable")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
            Directory.Delete(_temporaryRoot, true);
    }

    [Fact]
    public async Task LiveContext_IsNeverMappedToApiHostStorage()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<LiveWorkerFileAccessException>(() => storage.BrowseAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EFileContext.Live,
            string.Empty));
    }

    private FileSystemServerFileStorage CreateStorage()
        => new(
            new TestPathResolver(_temporaryRoot),
            new ServerFileClassifier(),
            new ServerFileLockProvider());

    private sealed class TestPathResolver(string root) : IPersistentStoragePathResolver
    {
        public string PersistentBasePath { get; } = Path.Combine(root, "persistent");

        public string ResolvePersistentRoot(Guid serverId)
            => Path.Combine(PersistentBasePath, serverId.ToString("N"));
    }
}
