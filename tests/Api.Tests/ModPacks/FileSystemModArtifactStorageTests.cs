using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using Xunit;

namespace Api.Tests.ModPacks;

public sealed class FileSystemModArtifactStorageTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"utf-mod-artifact-{Guid.NewGuid():N}");

    [Fact]
    public async Task DeleteAsync_RemovesPreviouslyStoredArtifact()
    {
        var storage = new FileSystemModArtifactStorage(new TestWebHostEnvironment(_root));
        await using var content = new MemoryStream([1, 2, 3]);
        await storage.StoreAsync("artifact-1", content);

        await storage.DeleteAsync("artifact-1");

        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync("artifact-1"));
    }

    [Fact]
    public async Task StoreAsync_RemovesTemporaryAndFinalFilesWhenInputFailsMidStream()
    {
        var storage = new FileSystemModArtifactStorage(new TestWebHostEnvironment(_root));
        await using var content = new ThrowAfterFirstReadStream([1, 2, 3]);

        await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync("artifact-failure", content));

        AssertStorageDirectoryIsEmpty();
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync("artifact-failure"));
    }

    [Fact]
    public async Task StoreAsync_RemovesTemporaryAndFinalFilesWhenCancelledMidStream()
    {
        var storage = new FileSystemModArtifactStorage(new TestWebHostEnvironment(_root));
        using var cancellation = new CancellationTokenSource();
        await using var content = new CancelAfterFirstReadStream([1, 2, 3], cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.StoreAsync("artifact-cancelled", content, cancellation.Token));

        AssertStorageDirectoryIsEmpty();
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync("artifact-cancelled"));
    }

    private void AssertStorageDirectoryIsEmpty()
    {
        var storageDirectory = Path.Combine(_root, "data", "mod-artifacts");
        Assert.True(Directory.Exists(storageDirectory));
        Assert.Empty(Directory.EnumerateFiles(storageDirectory));
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        return Task.CompletedTask;
    }

    /// <summary>Minimal web-host environment for filesystem storage tests.</summary>
    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        /// <inheritdoc />
        public string ApplicationName { get; set; } = "Api.Tests";
        /// <inheritdoc />
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        /// <inheritdoc />
        public string WebRootPath { get; set; } = root;
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = "Testing";
        /// <inheritdoc />
        public string ContentRootPath { get; set; } = root;
        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Throws after allowing one read to create a partial temporary artifact.</summary>
    private sealed class ThrowAfterFirstReadStream(byte[] content) : MemoryStream(content)
    {
        private bool _hasRead;

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_hasRead) throw new IOException("Simulated input failure.");
            _hasRead = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    /// <summary>Cancels the copy after allowing one read to create a partial temporary artifact.</summary>
    private sealed class CancelAfterFirstReadStream(byte[] content, CancellationTokenSource cancellation) : MemoryStream(content)
    {
        private bool _hasRead;

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_hasRead) cancellationToken.ThrowIfCancellationRequested();
            _hasRead = true;
            var result = base.ReadAsync(buffer, cancellationToken);
            cancellation.Cancel();
            return result;
        }
    }
}
