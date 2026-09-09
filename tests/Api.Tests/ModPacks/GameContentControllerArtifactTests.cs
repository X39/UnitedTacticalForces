using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ModPacks;

public sealed class GameContentControllerArtifactTests
{
    [Fact]
    public async Task CreateUploadedArtifactModAsync_DeletesArtifactWhenDatabaseCommitFails()
    {
        await using var dbContext = new FailingDatabaseContext(
            new DbContextOptionsBuilder<DatabaseContext>().Options);
        var storage = new RecordingArtifactStorage();
        var catalog = new GameServerDefinitionCatalog([new CounterStrike2GameServerDefinitionRegistration()]);
        var controller = new GameContentController(dbContext, catalog, null!, null!, [], storage);
        await using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "plugin.zip");

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateUploadedArtifactModAsync(
            "cs2", "Plugin", EModUsageRole.ServerOnly, "cs2-plugin", file, CancellationToken.None));

        Assert.NotNull(storage.StoredIdentifier);
        Assert.Equal(storage.StoredIdentifier, storage.DeletedIdentifier);
    }

    /// <summary>Database context that simulates a failed catalog commit.</summary>
    private sealed class FailingDatabaseContext(DbContextOptions<DatabaseContext> options) : DatabaseContext(options)
    {
        /// <inheritdoc />
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database failure.");
    }

    /// <summary>Records artifact compensation calls.</summary>
    private sealed class RecordingArtifactStorage : IModArtifactStorage
    {
        /// <summary>Gets the stored artifact identifier.</summary>
        public string? StoredIdentifier { get; private set; }
        /// <summary>Gets the deleted artifact identifier.</summary>
        public string? DeletedIdentifier { get; private set; }

        /// <inheritdoc />
        public Task StoreAsync(string artifactIdentifier, Stream content, CancellationToken cancellationToken = default)
        {
            StoredIdentifier = artifactIdentifier;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ModArtifactDownload> OpenReadAsync(
            string artifactIdentifier,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAsync(string artifactIdentifier, CancellationToken cancellationToken = default)
        {
            DeletedIdentifier = artifactIdentifier;
            return Task.CompletedTask;
        }
    }
}
