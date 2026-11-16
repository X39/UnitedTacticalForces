using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Verifies how the configuration service resolves game and control-plane settings.</summary>
public sealed class GameServerConfigurationServiceTests
{
    [Fact]
    public async Task Resolve_UsesDefaultsLatestProfileAndServerOverrideInOrder()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (server, profile, actor) = await SeedAsync(store);
        var first = AddRevision(profile, actor.PrimaryKey, 1, "Profile one");
        var latest = AddRevision(profile, actor.PrimaryKey, 2, "Profile two");
        store.DbContext.AddRange(first, latest);
        server.SelectedConfigurationProfile = profile;
        server.SelectedConfigurationProfileFk = profile.PrimaryKey;
        var serverValue = new GameServerConfigurationValue
        {
            PrimaryKey = Guid.NewGuid(),
            DefinitionKey = "server.cfg@hostname",
            ValueJson = JsonSerializer.Serialize("Server local"),
            UpdatedByFk = actor.PrimaryKey,
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        server.ConfigurationValues.Add(serverValue);
        store.DbContext.Add(serverValue);
        await store.DbContext.SaveChangesAsync();

        var service = new GameServerConfigurationService(store.DbContext);
        var resolved = await service.ResolveAsync(server.PrimaryKey);

        Assert.Equal(latest.PrimaryKey, resolved.ProfileRevisionId);
        Assert.Equal("Server local", resolved.Values["server.cfg@hostname"].GetString());
        Assert.Equal("Server", resolved.Entries.Single(q => q.Definition.Key == "server.cfg@hostname").Source);
        Assert.Equal(64, resolved.Values["server.cfg@maxPlayers"].GetInt32());

        server.PinnedConfigurationProfileRevisionFk = first.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var pinned = await service.ResolveAsync(server.PrimaryKey);
        Assert.Equal(first.PrimaryKey, pinned.ProfileRevisionId);
    }

    [Fact]
    public async Task Resolve_IgnoresIncompatibleHistoricalProfileValueAndKeepsRequiredTargetVisible()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (server, profile, actor) = await SeedAsync(store);
        var revision = AddRevision(profile, actor.PrimaryKey, 1, "Profile");
        store.DbContext.Add(revision);
        revision.Values.Add(new ConfigurationProfileValue
        {
            PrimaryKey = Guid.NewGuid(),
            DefinitionKey = "server.cfg@maxPlayers",
            ValueJson = JsonSerializer.Serialize("not-an-integer"),
        });
        server.SelectedConfigurationProfile = profile;
        await store.DbContext.SaveChangesAsync();

        var resolved = await new GameServerConfigurationService(store.DbContext).ResolveAsync(server.PrimaryKey);

        var maxPlayers = resolved.Entries.Single(q => q.Definition.Key == "server.cfg@maxPlayers");
        Assert.Equal("Default", maxPlayers.Source);
        Assert.Equal(64, maxPlayers.Value!.Value.GetInt32());
        Assert.Contains(resolved.Implementation.Targets, q => q.Path == Arma3ConfigurationImplementation.ServerTarget);
    }

    /// <summary>Verifies that deployment placement follows profile and override precedence.</summary>
    /// <returns>A task that completes after verifying effective placement and rendering.</returns>
    [Fact]
    public async Task Resolve_UsesEffectivePlacementAndExcludesItFromWorkerConfiguration()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (server, profile, actor) = await SeedAsync(store);
        var service = new GameServerConfigurationService(store.DbContext);
        Assert.Equal(
            Arma3ConfigurationImplementation.SeparateHostPlacement,
            Placement(await service.ResolveAsync(server.PrimaryKey)).Value!.Value.GetString());

        var pinned = AddRevision(profile, actor.PrimaryKey, 1, "Pinned");
        var latest = AddRevision(profile, actor.PrimaryKey, 2, "Latest");
        pinned.Values.Add(PlacementProfileValue(Arma3ConfigurationImplementation.SeparateHostPlacement));
        latest.Values.Add(PlacementProfileValue(Arma3ConfigurationImplementation.SameHostPlacement));
        store.DbContext.AddRange(pinned, latest);
        server.SelectedConfigurationProfile = profile;
        await store.DbContext.SaveChangesAsync();

        Assert.Equal(
            Arma3ConfigurationImplementation.SameHostPlacement,
            Placement(await service.ResolveAsync(server.PrimaryKey)).Value!.Value.GetString());

        server.PinnedConfigurationProfileRevisionFk = pinned.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal(
            Arma3ConfigurationImplementation.SeparateHostPlacement,
            Placement(await service.ResolveAsync(server.PrimaryKey)).Value!.Value.GetString());

        AddSameHostOverride(store, server, actor.PrimaryKey);
        await store.DbContext.SaveChangesAsync();

        var resolved = await service.ResolveAsync(server.PrimaryKey);
        var placement = Placement(resolved);
        Assert.Equal("Server", placement.Source);
        Assert.True(placement.IsCompatible);
        Assert.Equal(Arma3ConfigurationImplementation.SameHostPlacement, placement.Value!.Value.GetString());
        Assert.DoesNotContain(Arma3ConfigurationImplementation.HeadlessClientPlacementKey, resolved.Values.Keys);
        Assert.DoesNotContain(
            service.Render(resolved),
            q => Encoding.UTF8.GetString(q.Content)
                .Contains(Arma3ConfigurationImplementation.SameHostPlacement, StringComparison.Ordinal));
    }

    /// <summary>Verifies that a malformed effective placement cannot silently use the historical default.</summary>
    /// <returns>A task that completes after verifying profile and server validation.</returns>
    [Fact]
    public async Task Resolve_FlagsInvalidPlacementUnlessAValidServerOverrideTakesPrecedence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (server, profile, actor) = await SeedAsync(store);
        var service = new GameServerConfigurationService(store.DbContext);
        var revision = AddRevision(profile, actor.PrimaryKey, 1, "Invalid placement");
        revision.Values.Add(PlacementProfileValue("unknown-host"));
        store.DbContext.Add(revision);
        server.SelectedConfigurationProfile = profile;
        await store.DbContext.SaveChangesAsync();

        var invalid = Placement(await service.ResolveAsync(server.PrimaryKey));
        Assert.False(invalid.IsCompatible);
        Assert.NotNull(invalid.Warning);
        Assert.Throws<ArgumentException>(() => service.ValidateAndClone(
            server.GameIdentifier,
            Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
            JsonSerializer.SerializeToElement("unknown-host")));

        var serverValue = AddSameHostOverride(store, server, actor.PrimaryKey);
        await store.DbContext.SaveChangesAsync();

        var overridden = Placement(await service.ResolveAsync(server.PrimaryKey));
        Assert.True(overridden.IsCompatible);
        Assert.Null(overridden.Warning);

        serverValue.ValueJson = JsonSerializer.Serialize(false);
        await store.DbContext.SaveChangesAsync();
        Assert.False(Placement(await service.ResolveAsync(server.PrimaryKey)).IsCompatible);
    }

    /// <summary>Finds the control-plane placement entry, including any validation error.</summary>
    /// <param name="configuration">The effective configuration for the test server.</param>
    /// <returns>The resolved Headless Client placement entry.</returns>
    private static ResolvedConfigurationValue Placement(ResolvedGameConfiguration configuration)
        => configuration.Entries.Single(q => q.Definition.Key == Arma3ConfigurationImplementation.HeadlessClientPlacementKey);

    /// <summary>Builds a persisted profile entry for the Headless Client placement setting.</summary>
    /// <param name="value">The selection to persist.</param>
    /// <returns>A profile entry that can be attached to a test revision.</returns>
    private static ConfigurationProfileValue PlacementProfileValue(string value) => new()
    {
        PrimaryKey = Guid.NewGuid(),
        DefinitionKey = Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
        ValueJson = JsonSerializer.Serialize(value),
    };

    /// <summary>Tracks a same-host override for the given server.</summary>
    /// <param name="store">The store whose context tracks the test data.</param>
    /// <param name="server">The server receiving the override.</param>
    /// <param name="actorId">The user authoring the override.</param>
    /// <returns>The tracked override, which can be updated in the test.</returns>
    private static GameServerConfigurationValue AddSameHostOverride(HostingTestStore store, GameServer server, Guid actorId)
    {
        var serverValue = new GameServerConfigurationValue
        {
            PrimaryKey = Guid.NewGuid(),
            DefinitionKey = Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
            ValueJson = JsonSerializer.Serialize(Arma3ConfigurationImplementation.SameHostPlacement),
            UpdatedByFk = actorId,
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        server.ConfigurationValues.Add(serverValue);
        store.DbContext.Add(serverValue);
        return serverValue;
    }

    private static async Task<(GameServer server, ConfigurationProfile profile, User actor)> SeedAsync(
        HostingTestStore store)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var actor = new User { PrimaryKey = Guid.NewGuid(), Nickname = "config-test" };
        var owner = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.User,
            User = actor,
            DisplayName = actor.Nickname,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var server = new GameServer
        {
            PrimaryKey = Guid.NewGuid(),
            Title = "Configuration test",
            GameIdentifier = Arma3ConfigurationImplementation.Identifier,
            RuntimeProviderIdentifier = "test",
            RuntimeProfileIdentifier = "test",
            CreatedBy = actor,
            CurrentCostOwner = owner,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var profile = new ConfigurationProfile
        {
            PrimaryKey = Guid.NewGuid(),
            Title = "Profile",
            GameIdentifier = Arma3ConfigurationImplementation.Identifier,
            CreatedBy = actor,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.AddRange(actor, owner, server, profile);
        await store.DbContext.SaveChangesAsync();
        return (server, profile, actor);
    }

    private static ConfigurationProfileRevision AddRevision(
        ConfigurationProfile profile,
        Guid actorId,
        int number,
        string hostname)
    {
        var revision = new ConfigurationProfileRevision
        {
            PrimaryKey = Guid.NewGuid(),
            ConfigurationProfile = profile,
            RevisionNumber = number,
            CreatedByFk = actorId,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            Values =
            [
                new ConfigurationProfileValue
                {
                    PrimaryKey = Guid.NewGuid(),
                    DefinitionKey = "server.cfg@hostname",
                    ValueJson = JsonSerializer.Serialize(hostname),
                },
            ],
        };
        profile.Revisions.Add(revision);
        return revision;
    }
}
