using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerSecretServiceTests
{
    [Fact]
    public async Task Cs2_GeneratesRconEncryptsValuesAndRequiresExternalGslt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.Include(q => q.Secrets).SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        server.GameIdentifier = CounterStrike2GameServerDefinitionRegistration.GameIdentifier;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        server = await store.DbContext.GameServers.Include(q => q.Secrets).SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        var definition = new CounterStrike2GameServerDefinitionRegistration().Definition;
        var service = Create(store, definition);
        service.EnsureGeneratedSecrets(server, definition, seed.User.PrimaryKey, NodaSystemClock.Instance.GetCurrentInstant());
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        var missing = await Assert.ThrowsAsync<HostingValidationException>(
            () => service.ResolveRequiredAsync(seed.Server.PrimaryKey));
        Assert.Contains("Steam Game Server Login Token", missing.Message, StringComparison.Ordinal);

        await service.SetAsync(seed.Server.PrimaryKey, "gslt", "external-token", EGameServerSecretOrigin.ExternalProvider, seed.User.PrimaryKey);
        var resolved = await service.ResolveRequiredAsync(seed.Server.PrimaryKey);
        Assert.Equal("external-token", resolved["gslt"]);
        Assert.Equal(64, resolved["rcon-password"].Length);
        var row = await store.DbContext.GameServerSecrets.AsNoTracking()
            .SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey && q.Key == "rcon-password" && q.IsCurrent);
        Assert.DoesNotContain(resolved["rcon-password"], row.ProtectedValue, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedSecretRotation_CreatesAnImmutableNewVersion()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.Include(q => q.Secrets).SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        server.GameIdentifier = CounterStrike2GameServerDefinitionRegistration.GameIdentifier;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        server = await store.DbContext.GameServers.Include(q => q.Secrets).SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        var definition = new CounterStrike2GameServerDefinitionRegistration().Definition;
        var service = Create(store, definition);
        service.EnsureGeneratedSecrets(server, definition, seed.User.PrimaryKey, NodaSystemClock.Instance.GetCurrentInstant());
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var before = await service.RevealAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);

        await service.RotateAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);
        var after = await service.RevealAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);

        Assert.Equal(1, before.Version);
        Assert.Equal(2, after.Version);
        Assert.NotEqual(before.Value, after.Value);
        Assert.Equal(2, await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey && q.Key == "rcon-password"));
    }

    private static GameServerSecretService Create(HostingTestStore store, GameServerDefinition definition)
        => new(
            store.DbContext,
            new GameServerDefinitionCatalog([new Registration(definition)]),
            new EphemeralDataProtectionProvider(),
            new HostingAuditWriter());

    private sealed record Registration(GameServerDefinition Definition) : IGameServerDefinitionRegistration;
}
