using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerContentSnapshotResolverTests
{
    [Fact]
    public async Task Selection_ResolvesOnlyActiveNormalizedRevision()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = "arma3";
        var definition = Definition(seed.User.PrimaryKey, "Pack", "arma3");
        var mod = WorkshopMod("Active mod", "450814997");
        _ = Revision(seed.User.PrimaryKey, definition, true, mod);
        _ = Revision(seed.User.PrimaryKey, definition, false, WorkshopMod("Old mod", "463939057"));
        seed.Server.SelectedModPackDefinition = definition;
        store.DbContext.AddRange(definition, mod);
        await store.DbContext.SaveChangesAsync();

        var content = await new GameServerContentSnapshotResolver(store.DbContext).ResolveAsync(seed.Server.PrimaryKey);

        var reference = Assert.Single(content);
        Assert.Equal(ModSourceProviderIdentifiers.SteamWorkshop, reference.Source);
        Assert.Equal("450814997", reference.Identifier);
        Assert.Equal("Active mod", reference.DisplayName);
    }

    [Fact]
    public async Task NestedSelection_DeduplicatesCatalogMods()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = "arma3";
        var shared = WorkshopMod("CBA", "450814997");
        var first = Definition(seed.User.PrimaryKey, "First", "arma3");
        _ = Revision(seed.User.PrimaryKey, first, true, shared, WorkshopMod("ACE", "463939057"));
        var second = Definition(seed.User.PrimaryKey, "Second", "arma3");
        _ = Revision(seed.User.PrimaryKey, second, true, shared, WorkshopMod("TFAR", "894678801"));
        var root = Definition(seed.User.PrimaryKey, "Root", "arma3");
        var rootRevision = Revision(seed.User.PrimaryKey, root, true);
        rootRevision.Dependencies =
        [
            new() { ChildDefinition = first, Order = 0 },
            new() { ChildDefinition = second, Order = 1 },
        ];
        seed.Server.SelectedModPackDefinition = root;
        store.DbContext.AddRange(root, first, second, shared);
        await store.DbContext.SaveChangesAsync();

        var content = await new GameServerContentSnapshotResolver(store.DbContext).ResolveAsync(seed.Server.PrimaryKey);

        Assert.Equal(["450814997", "463939057", "894678801"], content.Select(q => q.Identifier));
        Assert.Single(content.Where(q => q.Identifier == "450814997"));
    }

    [Fact]
    public async Task SelectionWithoutNormalizedMembers_ReturnsEmpty()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = "arma3";
        var definition = Definition(seed.User.PrimaryKey, "Empty", "arma3");
        _ = Revision(seed.User.PrimaryKey, definition, true);
        seed.Server.SelectedModPackDefinition = definition;
        store.DbContext.Add(definition);
        await store.DbContext.SaveChangesAsync();

        var content = await new GameServerContentSnapshotResolver(store.DbContext).ResolveAsync(seed.Server.PrimaryKey);

        Assert.Empty(content);
    }

    [Fact]
    public async Task GitDefaultBranch_PreservesCanonicalUrlAndBlankVersionSelector()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = "arma3";
        var definition = Definition(seed.User.PrimaryKey, "Git pack", "arma3");
        var mod = new Mod
        {
            PrimaryKey = Guid.NewGuid(),
            GameIdentifier = "arma3",
            ProviderIdentifier = ModSourceProviderIdentifiers.Git,
            CanonicalSourceIdentifier = "https://github.com/example/public-mod.git",
            Title = "Git mod",
            UsageRole = EModUsageRole.Shared,
            IsActive = true,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        mod.Versions.Add(new ModVersion
        {
            PrimaryKey = Guid.NewGuid(),
            Mod = mod,
            ModFk = mod.PrimaryKey,
            VersionNumber = 1,
            VersionLabel = "Default branch",
            VersionSelector = string.Empty,
            InstallerIdentifier = "arma3-overlay",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        });
        _ = Revision(seed.User.PrimaryKey, definition, true, mod);
        seed.Server.SelectedModPackDefinition = definition;
        store.DbContext.AddRange(definition, mod);
        await store.DbContext.SaveChangesAsync();

        var reference = Assert.Single(
            await new GameServerContentSnapshotResolver(store.DbContext).ResolveAsync(seed.Server.PrimaryKey));

        Assert.Equal("https://github.com/example/public-mod.git", reference.Identifier);
        Assert.Equal(string.Empty, reference.VersionIdentifier);
    }

    [Fact]
    public async Task NestedCrossGameDefinition_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = "arma3";
        var other = Definition(seed.User.PrimaryKey, "Other", "other-game");
        _ = Revision(seed.User.PrimaryKey, other, true);
        var root = Definition(seed.User.PrimaryKey, "Root", "arma3");
        var revision = Revision(seed.User.PrimaryKey, root, true);
        revision.Dependencies = [new() { ChildDefinition = other, Order = 0 }];
        seed.Server.SelectedModPackDefinition = root;
        store.DbContext.AddRange(root, other);
        await store.DbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() =>
            new GameServerContentSnapshotResolver(store.DbContext).ResolveAsync(seed.Server.PrimaryKey));

        Assert.Equal("content-selection-invalid", exception.FailureCode);
    }

    private static ModPackDefinition Definition(Guid ownerId, string title, string gameIdentifier)
        => new()
        {
            Title = title, GameIdentifier = gameIdentifier, OwnerFk = ownerId, IsActive = true,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(), ModPackRevisionsOwned = [],
        };

    private static ModPackRevision Revision(Guid actorId, ModPackDefinition definition, bool isActive, params Mod[] mods)
    {
        var revision = new ModPackRevision
        {
            Definition = definition, UpdatedByFk = actorId, IsActive = isActive,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            Mods = mods.Select((q, index) => new ModPackRevisionMod { Mod = q, Order = index }).ToList(),
        };
        definition.ModPackRevisionsOwned!.Add(revision);
        return revision;
    }

    private static Mod WorkshopMod(string title, string identifier)
    {
        var mod = new Mod
        {
            PrimaryKey = Guid.NewGuid(), GameIdentifier = "arma3",
            ProviderIdentifier = ModSourceProviderIdentifiers.SteamWorkshop,
            CanonicalSourceIdentifier = identifier, Title = title, UsageRole = EModUsageRole.Shared, IsActive = true,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        mod.Versions.Add(new ModVersion
        {
            PrimaryKey = Guid.NewGuid(), Mod = mod, ModFk = mod.PrimaryKey, VersionNumber = 1,
            VersionLabel = "latest", InstallerIdentifier = "arma3-workshop",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        });
        return mod;
    }
}
