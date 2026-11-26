using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Verifies the mod pack contents projection, the availability of mod pack options and their authorization rules.
/// </summary>
public sealed class GameServerModPackContentTests
{
    private const string Arma3 = Arma3GameServerDefinitionRegistration.GameIdentifier;

    /// <summary>Verifies that an active pack returns its mods in membership order with comparison keys.</summary>
    [Fact]
    public async Task GetContentsAsync_ForActivePack_ReturnsOrderedKeyedItems()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var first = NewMod("450814997", "CBA", Arma3);
        var second = NewMod("463939057", "ACE", Arma3);
        var pack = NewPack("Main pack", Arma3, seed, true, first, second);
        store.DbContext.ModPackDefinitions.Add(pack);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            pack.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.True(contents.IsAvailable);
        Assert.Null(contents.UnavailableReason);
        Assert.Equal(pack.PrimaryKey, contents.ModPackDefinitionId);
        Assert.Equal(2, contents.Total);
        Assert.Equal(
            ["steam-workshop:450814997", "steam-workshop:463939057"],
            contents.Items.Select(q => q.Key).ToArray());
        Assert.Equal(["CBA", "ACE"], contents.Items.Select(q => q.Title).ToArray());
    }

    /// <summary>Verifies that a mod reachable twice through a dependency yields a single item.</summary>
    [Fact]
    public async Task GetContentsAsync_WithSharedModThroughDependency_ReturnsOneItem()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var shared = NewMod("450814997", "CBA", Arma3);
        var child = NewPack("Child pack", Arma3, seed, true, shared);
        var parent = NewPack("Parent pack", Arma3, seed, true, shared);
        store.DbContext.ModPackDefinitions.AddRange(child, parent);
        await store.DbContext.SaveChangesAsync();
        var parentRevision = Assert.Single(parent.ModPackRevisionsOwned!);
        parentRevision.Dependencies =
        [
            new ModPackRevisionDependency { ChildDefinitionFk = child.PrimaryKey, Order = 0 },
        ];
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            parent.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.Equal(1, contents.Total);
        Assert.Equal("steam-workshop:450814997", Assert.Single(contents.Items).Key);
    }

    /// <summary>Verifies that duplicate keys from the resolver collapse to the first occurrence.</summary>
    [Fact]
    public async Task GetContentsAsync_WithDuplicateKeysFromResolver_KeepsFirstOccurrence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var pack = NewPack("Main pack", Arma3, seed, true, NewMod("1", "One", Arma3));
        store.DbContext.ModPackDefinitions.Add(pack);
        await store.DbContext.SaveChangesAsync();
        var resolver = new StubResolver(
            Reference("steam-workshop", "1", "First"),
            Reference("steam-workshop", "1", "Second"),
            Reference("steam-workshop", "2", "Other"));
        var service = new GameServerModPackContentService(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            resolver);

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            pack.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.Equal(2, contents.Total);
        Assert.Equal(["First", "Other"], contents.Items.Select(q => q.Title).ToArray());
    }

    /// <summary>Verifies that a deactivated pack reports itself unavailable instead of failing.</summary>
    [Fact]
    public async Task GetContentsAsync_ForInactivePack_ReportsInactive()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var pack = NewPack("Old pack", Arma3, seed, false);
        store.DbContext.ModPackDefinitions.Add(pack);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            pack.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.False(contents.IsAvailable);
        Assert.Equal(ModPackAvailabilityReasons.Inactive, contents.UnavailableReason);
        Assert.Equal(0, contents.Total);
        Assert.Empty(contents.Items);
    }

    /// <summary>Verifies that an active pack without an active revision reports itself unavailable.</summary>
    [Fact]
    public async Task GetContentsAsync_ForPackWithoutActiveRevision_ReportsNoActiveRevision()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var pack = NewPack("Empty pack", Arma3, seed, true);
        pack.ModPackRevisionsOwned!.Clear();
        store.DbContext.ModPackDefinitions.Add(pack);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            pack.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.False(contents.IsAvailable);
        Assert.Equal(ModPackAvailabilityReasons.NoActiveRevision, contents.UnavailableReason);
        Assert.Empty(contents.Items);
    }

    /// <summary>Verifies that an unresolvable member mod yields a safe reason and no exception.</summary>
    [Fact]
    public async Task GetContentsAsync_ForInactiveMemberMod_ReportsUnresolvable()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var mod = NewMod("450814997", "CBA", Arma3);
        mod.IsActive = false;
        var pack = NewPack("Broken pack", Arma3, seed, true, mod);
        store.DbContext.ModPackDefinitions.Add(pack);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            pack.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.False(contents.IsAvailable);
        Assert.Equal(ModPackAvailabilityReasons.Unresolvable, contents.UnavailableReason);
    }

    /// <summary>Verifies that a curated resolver message is surfaced as the reason.</summary>
    [Fact]
    public async Task GetContentsAsync_ForPermanentResolverFailure_ReturnsItsCuratedMessage()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var pack = NewPack("Main pack", Arma3, seed, true, NewMod("1", "One", Arma3));
        store.DbContext.ModPackDefinitions.Add(pack);
        await store.DbContext.SaveChangesAsync();
        var service = new GameServerModPackContentService(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            new StubResolver(new PermanentLifecycleExecutionException("The selected mod pack graph contains a cycle.")));

        var contents = await service.GetContentsAsync(
            seed.Server.PrimaryKey,
            pack.PrimaryKey,
            ViewPrincipal(seed));

        Assert.NotNull(contents);
        Assert.False(contents.IsAvailable);
        Assert.Equal("The selected mod pack graph contains a cycle.", contents.UnavailableReason);
    }

    /// <summary>Verifies that lookups outside the server's scope return nothing.</summary>
    [Fact]
    public async Task GetContentsAsync_OutsideScope_ReturnsNull()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var pack = NewPack("Main pack", Arma3, seed, true, NewMod("1", "One", Arma3));
        var otherGame = NewPack("Other game pack", "another-game", seed, true);
        store.DbContext.ModPackDefinitions.AddRange(pack, otherGame);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        Assert.Null(await service.GetContentsAsync(
            seed.Server.PrimaryKey, otherGame.PrimaryKey, ViewPrincipal(seed)));
        Assert.Null(await service.GetContentsAsync(
            seed.Server.PrimaryKey, 987_654, ViewPrincipal(seed)));
        Assert.Null(await service.GetContentsAsync(
            seed.Server.PrimaryKey, pack.PrimaryKey, HostingTestData.Principal(seed.User)));

        seed.Server.IsArchived = true;
        await store.DbContext.SaveChangesAsync();
        Assert.Null(await service.GetContentsAsync(
            seed.Server.PrimaryKey, pack.PrimaryKey, ViewPrincipal(seed)));
    }

    /// <summary>Verifies that options mark availability and keep the saved inactive pack listed.</summary>
    [Fact]
    public async Task GetModPackOptionsAsync_MarksAvailabilityAndKeepsSavedInactivePack()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3;
        var usable = NewPack("A usable", Arma3, seed, true);
        var noRevision = NewPack("B no revision", Arma3, seed, true);
        noRevision.ModPackRevisionsOwned!.Clear();
        var savedInactive = NewPack("C saved inactive", Arma3, seed, false);
        var otherInactive = NewPack("D other inactive", Arma3, seed, false);
        var otherGame = NewPack("E other game", "another-game", seed, true);
        store.DbContext.ModPackDefinitions.AddRange(usable, noRevision, savedInactive, otherInactive, otherGame);
        await store.DbContext.SaveChangesAsync();
        seed.Server.SelectedModPackDefinitionFk = savedInactive.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateProjectionService(store.DbContext);

        var options = await service.GetModPackOptionsAsync(seed.Server.PrimaryKey, ViewPrincipal(seed));

        Assert.Equal(
            ["A usable", "B no revision", "C saved inactive"],
            options.Select(q => q.Title).ToArray());
        var byTitle = options.ToDictionary(q => q.Title);
        Assert.True(byTitle["A usable"].IsAvailable);
        Assert.Null(byTitle["A usable"].UnavailableReason);
        Assert.False(byTitle["B no revision"].IsAvailable);
        Assert.Equal(ModPackAvailabilityReasons.NoActiveRevision, byTitle["B no revision"].UnavailableReason);
        Assert.False(byTitle["C saved inactive"].IsAvailable);
        Assert.Equal(ModPackAvailabilityReasons.Inactive, byTitle["C saved inactive"].UnavailableReason);
    }

    /// <summary>Verifies that creation options project availability for their active packs.</summary>
    [Fact]
    public async Task GetCreationModPackOptionsAsync_MarksAvailability()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var usable = NewPack("A usable", Arma3, seed, true);
        var noRevision = NewPack("B no revision", Arma3, seed, true);
        noRevision.ModPackRevisionsOwned!.Clear();
        var inactive = NewPack("C inactive", Arma3, seed, false);
        store.DbContext.ModPackDefinitions.AddRange(usable, noRevision, inactive);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateProjectionService(store.DbContext);

        var options = await service.GetCreationModPackOptionsAsync(Arma3);

        Assert.Equal(["A usable", "B no revision"], options.Select(q => q.Title).ToArray());
        Assert.True(options.First().IsAvailable);
        Assert.Null(options.First().UnavailableReason);
        Assert.False(options.Last().IsAvailable);
        Assert.Equal(ModPackAvailabilityReasons.NoActiveRevision, options.Last().UnavailableReason);
    }

    /// <summary>Verifies the controller maps the service result to 200 and 404.</summary>
    [Fact]
    public async Task GetModPackContentsAsync_MapsServiceResultToStatus()
    {
        var controller = new GameServersController(null!, null!, null!);
        var dto = new GameServerModPackContentsDto { ModPackDefinitionId = 5, IsAvailable = true };

        var ok = await controller.GetModPackContentsAsync(
            Guid.NewGuid(), 5, new StubContentService(dto), CancellationToken.None);
        var missing = await controller.GetModPackContentsAsync(
            Guid.NewGuid(), 5, new StubContentService(null), CancellationToken.None);

        Assert.Same(dto, Assert.IsType<OkObjectResult>(ok).Value);
        Assert.IsType<NotFoundResult>(missing);
    }

    private static GameServerModPackContentService CreateService(HostingTestStore store)
        => new(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            new GameServerContentSnapshotResolver(store.DbContext));

    private static ClaimsPrincipal ViewPrincipal(HostingTestSeed seed)
        => HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

    private static WorkerContentReference Reference(string source, string identifier, string displayName)
        => new() { Source = source, Identifier = identifier, DisplayName = displayName };

    private static Mod NewMod(string identifier, string title, string gameIdentifier)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var mod = new Mod
        {
            PrimaryKey = Guid.NewGuid(),
            GameIdentifier = gameIdentifier,
            ProviderIdentifier = ModSourceProviderIdentifiers.SteamWorkshop,
            CanonicalSourceIdentifier = identifier,
            Title = title,
            UsageRole = EModUsageRole.Shared,
            IsActive = true,
            TimeStampCreated = now,
        };
        mod.Versions.Add(new ModVersion
        {
            PrimaryKey = Guid.NewGuid(),
            Mod = mod,
            ModFk = mod.PrimaryKey,
            VersionNumber = 1,
            VersionLabel = "Current Workshop version",
            VersionSelector = string.Empty,
            InstallerIdentifier = "arma3-workshop",
            TimeStampCreated = now,
        });
        return mod;
    }

    private static ModPackDefinition NewPack(
        string title,
        string gameIdentifier,
        HostingTestSeed seed,
        bool isActive,
        params Mod[] mods)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var definition = new ModPackDefinition
        {
            Title = title,
            GameIdentifier = gameIdentifier,
            OwnerFk = seed.User.PrimaryKey,
            IsActive = isActive,
            TimeStampCreated = now,
            ModPackRevisionsOwned = [],
        };
        definition.ModPackRevisionsOwned.Add(new ModPackRevision
        {
            Definition = definition,
            UpdatedByFk = seed.User.PrimaryKey,
            IsActive = true,
            TimeStampCreated = now,
            Mods = mods
                .Select((mod, index) => new ModPackRevisionMod { Mod = mod, ModFk = mod.PrimaryKey, Order = index })
                .ToList(),
        });
        return definition;
    }

    private sealed class StubResolver : IGameServerContentSnapshotResolver
    {
        private readonly IReadOnlyCollection<WorkerContentReference> _references;
        private readonly Exception? _failure;

        public StubResolver(params WorkerContentReference[] references)
        {
            _references = references;
        }

        public StubResolver(Exception failure)
        {
            _references = [];
            _failure = failure;
        }

        public Task<IReadOnlyCollection<WorkerContentReference>> ResolveAsync(
            Guid gameServerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<WorkerContentReference>> ResolveAsync(
            string gameIdentifier,
            long? modPackDefinitionId,
            CancellationToken cancellationToken = default)
            => _failure is null ? Task.FromResult(_references) : throw _failure;
    }

    private sealed class StubContentService(GameServerModPackContentsDto? result) : IGameServerModPackContentService
    {
        public Task<GameServerModPackContentsDto?> GetContentsAsync(
            Guid gameServerId,
            long modPackDefinitionId,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
