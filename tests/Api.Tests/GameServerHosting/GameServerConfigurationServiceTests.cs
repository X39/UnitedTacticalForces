using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

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
