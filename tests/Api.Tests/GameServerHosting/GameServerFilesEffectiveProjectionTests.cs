using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerFilesEffectiveProjectionTests
{
    [Fact]
    public async Task BattleyeDirectory_ListsGeneratedFileWhenPhysicalDirectoryDoesNotExist()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var server = await SeedServerAsync(store);
        var controller = CreateController(store, server, new ProjectionStorage { DirectoryMissing = true });

        var result = await controller.BrowseAsync(
            server.PrimaryKey, "persistent", "server/battleye", CancellationToken.None);

        var files = Assert.IsType<HostingFileDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        var generated = Assert.Single(files);
        Assert.Equal("beserver.cfg", generated.Name);
        Assert.True(generated.IsGeneratedTarget);
        Assert.Equal(EHostingFileOrigin.Generated, generated.Origin);
    }

    [Fact]
    public async Task BattleyeDirectory_MixesPhysicalFilesAndReturnsOneEffectiveCollision()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var server = await SeedServerAsync(store);
        var storage = new ProjectionStorage
        {
            Entries =
            [
                Entry("server/battleye/bans.txt"),
                Entry("server/battleye/beserver.cfg"),
            ],
        };
        var controller = CreateController(store, server, storage);

        var result = await controller.BrowseAsync(
            server.PrimaryKey, "persistent", "server/battleye", CancellationToken.None);

        var files = Assert.IsType<HostingFileDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, files.Length);
        var effective = Assert.Single(files.Where(q => q.Name == "beserver.cfg"));
        Assert.True(effective.OverridesGenerated);
        Assert.Equal(EHostingFileOrigin.PersistentOverride, effective.Origin);
        Assert.Contains(files, q => q.Name == "bans.txt" && !q.IsGeneratedTarget);
    }

    private static GameServerFilesController CreateController(
        HostingTestStore store,
        GameServer server,
        IServerFileStorage storage)
    {
        var controller = new GameServerFilesController(
            store.DbContext,
            storage,
            new ServerFileClassifier(),
            new GameServerDefinitionCatalog([new Arma3GameServerDefinitionRegistration()]),
            new GameServerConfigurationService(store.DbContext),
            new PreservationSelectionResolver(),
            new HostingAuthorizationService(store.DbContext),
            new HostingAuditWriter(),
            NullLogger<GameServerFilesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new SecurityClaim(Claims.ResourceBased.Server.All, server.PrimaryKey.ToString()),
                ], "test")),
            },
        };
        return controller;
    }

    private static async Task<GameServer> SeedServerAsync(HostingTestStore store)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var user = new User { PrimaryKey = Guid.NewGuid(), Nickname = "file-test" };
        var owner = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.User,
            User = user,
            DisplayName = user.Nickname,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var server = new GameServer
        {
            PrimaryKey = Guid.NewGuid(),
            Title = "File projection",
            GameIdentifier = Arma3ConfigurationImplementation.Identifier,
            RuntimeProviderIdentifier = "test",
            RuntimeProfileIdentifier = "test",
            CreatedBy = user,
            CurrentCostOwner = owner,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.AddRange(user, owner, server);
        await store.DbContext.SaveChangesAsync();
        return server;
    }

    private static ServerFileEntry Entry(string path)
        => new(
            EFileContext.Persistent,
            path,
            Path.GetFileName(path),
            false,
            10,
            DateTimeOffset.UtcNow,
            EFileClassification.Configuration,
            true);

    private sealed class ProjectionStorage : IServerFileStorage
    {
        public bool DirectoryMissing { get; init; }
        public IReadOnlyList<ServerFileEntry> Entries { get; init; } = [];

        public Task<IReadOnlyList<ServerFileEntry>> BrowseAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => DirectoryMissing
                ? Task.FromException<IReadOnlyList<ServerFileEntry>>(new DirectoryNotFoundException())
                : Task.FromResult(Entries);

        public Task<ServerFileDownload> OpenReadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServerFileEntry> UploadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, Stream content,
            bool overwrite, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServerFileEntry> CreateDirectoryAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, bool recursive,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
