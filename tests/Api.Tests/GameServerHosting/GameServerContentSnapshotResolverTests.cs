using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Core;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerContentSnapshotResolverTests
{
    [Fact]
    public async Task StandaloneSelection_ResolvesItsUniqueActiveRevision()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        var definition = Definition(seed.User.PrimaryKey, "Standalone", "arma3", isComposition: false);
        var active = Revision(
            seed.User.PrimaryKey,
            definition,
            isActive: true,
            Preset("Active", ("Active mod", 450814997)));
        _ = Revision(
            seed.User.PrimaryKey,
            definition,
            isActive: false,
            Preset("Old", ("Old mod", 463939057)));
        seed.Server.SelectedModPackDefinition = definition;
        store.DbContext.ModPackDefinitions.Add(definition);
        await store.DbContext.SaveChangesAsync();
        var resolver = new GameServerContentSnapshotResolver(store.DbContext);

        var content = await resolver.ResolveAsync(seed.Server.PrimaryKey);

        var reference = Assert.Single(content);
        Assert.Equal("steam-workshop", reference.Source);
        Assert.Equal("450814997", reference.Identifier);
        Assert.Equal("Active mod", reference.DisplayName);
        Assert.True(active.IsActive);
    }

    [Fact]
    public async Task CompositionSelection_ResolvesSelectedMembersAndDeduplicatesWorkshopIds()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        var first = Definition(seed.User.PrimaryKey, "First", "arma3", isComposition: false);
        var firstRevision = Revision(
            seed.User.PrimaryKey,
            first,
            isActive: true,
            Preset("First", ("CBA", 450814997), ("ACE", 463939057)));
        var second = Definition(seed.User.PrimaryKey, "Second", "arma3", isComposition: false);
        var secondRevision = Revision(
            seed.User.PrimaryKey,
            second,
            isActive: true,
            Preset("Second", ("Duplicate CBA", 450814997), ("TFAR", 894678801)));
        var composition = Definition(seed.User.PrimaryKey, "Composition", "arma3", isComposition: true);
        composition.ModPackRevisions = [firstRevision, secondRevision];
        seed.Server.SelectedModPackDefinition = composition;
        store.DbContext.ModPackDefinitions.AddRange(first, second, composition);
        await store.DbContext.SaveChangesAsync();
        var resolver = new GameServerContentSnapshotResolver(store.DbContext);

        var content = await resolver.ResolveAsync(seed.Server.PrimaryKey);

        Assert.Equal(["450814997", "463939057", "894678801"], content.Select(q => q.Identifier));
        Assert.Equal("CBA", content.Single(q => q.Identifier == "450814997").DisplayName);
    }

    [Fact]
    public async Task SelectionWithNonSteamWorkshopLink_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        var definition = Definition(seed.User.PrimaryKey, "Unsafe", "arma3", isComposition: false);
        _ = Revision(
            seed.User.PrimaryKey,
            definition,
            isActive: true,
            Arma3ModPackParser.ToHtml(
                "Unsafe",
                [new Arma3ModPackParser.TitleLinkPair("Not Steam", "https://example.test/mod?id=42")],
                []));
        seed.Server.SelectedModPackDefinition = definition;
        store.DbContext.ModPackDefinitions.Add(definition);
        await store.DbContext.SaveChangesAsync();
        var resolver = new GameServerContentSnapshotResolver(store.DbContext);

        var exception = await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() =>
            resolver.ResolveAsync(seed.Server.PrimaryKey));

        Assert.Equal("content-selection-invalid", exception.FailureCode);
    }

    [Fact]
    public async Task CompositionWithCrossGameMember_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        var otherGame = Definition(seed.User.PrimaryKey, "Other", "other-game", isComposition: false);
        var otherRevision = Revision(
            seed.User.PrimaryKey,
            otherGame,
            isActive: true,
            Preset("Other", ("Other mod", 450814997)));
        var composition = Definition(seed.User.PrimaryKey, "Composition", "arma3", isComposition: true);
        composition.ModPackRevisions = [otherRevision];
        seed.Server.SelectedModPackDefinition = composition;
        store.DbContext.ModPackDefinitions.AddRange(otherGame, composition);
        await store.DbContext.SaveChangesAsync();
        var resolver = new GameServerContentSnapshotResolver(store.DbContext);

        var exception = await Assert.ThrowsAsync<PermanentLifecycleExecutionException>(() =>
            resolver.ResolveAsync(seed.Server.PrimaryKey));

        Assert.Equal("content-selection-invalid", exception.FailureCode);
    }

    private static ModPackDefinition Definition(
        Guid ownerId,
        string title,
        string gameIdentifier,
        bool isComposition)
        => new()
        {
            Title = title,
            GameIdentifier = gameIdentifier,
            OwnerFk = ownerId,
            IsActive = true,
            IsComposition = isComposition,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            ModPackRevisions = [],
            ModPackRevisionsOwned = [],
        };

    private static ModPackRevision Revision(
        Guid actorId,
        ModPackDefinition definition,
        bool isActive,
        string html)
    {
        var revision = new ModPackRevision
        {
            Definition = definition,
            UpdatedByFk = actorId,
            IsActive = isActive,
            Html = html,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            ModPackDefinitions = [],
        };
        definition.ModPackRevisionsOwned!.Add(revision);
        return revision;
    }

    private static string Preset(string title, params (string Title, ulong WorkshopId)[] mods)
        => Arma3ModPackParser.ToHtml(
            title,
            mods.Select(q => new Arma3ModPackParser.TitleLinkPair(
                q.Title,
                $"https://steamcommunity.com/sharedfiles/filedetails/?id={q.WorkshopId}")),
            []);
}
