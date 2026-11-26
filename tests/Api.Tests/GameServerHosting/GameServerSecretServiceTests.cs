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

    /// <summary>
    /// Verifies that a rotation returns the generated value and version once, that the value equals what a later
    /// reveal returns, and that neither the audit summary nor its details carry the value.
    /// </summary>
    [Fact]
    public async Task RotateAsync_ReturnsTheGeneratedValueAndAuditsWithoutIt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);

        var rotated = await service.RotateAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);
        var revealed = await service.RevealAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);

        Assert.Equal("rcon-password", rotated.Key);
        Assert.Equal(64, rotated.Value.Length);
        Assert.Equal(2, rotated.Version);
        Assert.Equal(rotated.Value, revealed.Value);
        var audit = await store.DbContext.HostingAuditRecords.AsNoTracking().SingleAsync(
            q => q.GameServerFk == seed.Server.PrimaryKey && q.Kind == EHostingAuditEventKind.ServerSecretRotated);
        Assert.DoesNotContain(rotated.Value, audit.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(rotated.Value, audit.Details ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Version=2", audit.Details, StringComparison.Ordinal);
        Assert.Contains("rcon-password", audit.Details, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a game-declared chosen value replaces a generated credential as a user-provided version and
    /// marks the credentials area as pending.
    /// </summary>
    [Fact]
    public async Task SetAsync_ChosenValueForGeneratedSecret_StoresUserProvidedVersion()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);

        await service.SetAsync(
            seed.Server.PrimaryKey, "rcon-password", "Chosen-Value_42", EGameServerSecretOrigin.UserProvided, seed.User.PrimaryKey);
        await service.SetAsync(
            seed.Server.PrimaryKey, "gslt", "external-token", EGameServerSecretOrigin.ExternalProvider, seed.User.PrimaryKey);

        var resolved =await service.ResolveRequiredAsync(seed.Server.PrimaryKey, CancellationToken.None);
        Assert.Equal("Chosen-Value_42", resolved["rcon-password"]);
        var current = await store.DbContext.GameServerSecrets.AsNoTracking()
            .SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey && q.Key == "rcon-password" && q.IsCurrent);
        Assert.Equal(2, current.Version);
        Assert.Equal(EGameServerSecretOrigin.UserProvided, current.Origin);
        var server = await store.DbContext.GameServers.AsNoTracking().SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Credentials, server.PendingStartChangeAreas);
    }

    /// <summary>Verifies that a generated credential without the chosen-value declaration stays platform-managed.</summary>
    [Fact]
    public async Task SetAsync_GeneratedSecretWithoutChosenValueDeclaration_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, _) = await PrepareCounterStrike2Async(store, archived: false);
        var definition = new CounterStrike2GameServerDefinitionRegistration().Definition;
        var strict = definition with
        {
            Secrets = definition.Secrets
                .Select(q => q.Key == "rcon-password" ? q with { AcceptsChosenValue = false } : q)
                .ToArray(),
        };
        var service = Create(store, strict);
        var versionsBefore = await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey);

        var exception = await Assert.ThrowsAsync<HostingValidationException>(() => service.SetAsync(
            seed.Server.PrimaryKey, "rcon-password", "Chosen-Value_42", EGameServerSecretOrigin.UserProvided, seed.User.PrimaryKey));

        Assert.Equal("This secret is managed by the platform.", exception.Message);
        Assert.Equal(versionsBefore, await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey));
    }

    /// <summary>
    /// Verifies that the token format rejects whitespace, quotes, backslashes, non-ASCII characters and control
    /// characters without echoing the value or adding a version.
    /// </summary>
    /// <param name="value">The rejected value.</param>
    [Theory]
    [InlineData("two words")]
    [InlineData("has\"quote")]
    [InlineData("has\\backslash")]
    [InlineData("ümlaut1")]
    [InlineData("ab\tcd")]
    public async Task SetAsync_TokenCredentialWithUnusableValue_IsRejectedWithoutEchoingTheValue(string value)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);
        var versionsBefore = await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey);

        var exception = await Assert.ThrowsAsync<HostingValidationException>(() => service.SetAsync(
            seed.Server.PrimaryKey, "rcon-password", value, EGameServerSecretOrigin.UserProvided, seed.User.PrimaryKey));

        Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
        Assert.Equal(versionsBefore, await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey));
    }

    /// <summary>Verifies that a single-line credential accepts spaces but rejects line breaks.</summary>
    [Fact]
    public async Task SetAsync_SingleLineCredential_AcceptsSpacesAndRejectsLineBreaks()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);

        await service.SetAsync(
            seed.Server.PrimaryKey, "join-password", "two words", EGameServerSecretOrigin.UserProvided, seed.User.PrimaryKey);
        var exception = await Assert.ThrowsAsync<HostingValidationException>(() => service.SetAsync(
            seed.Server.PrimaryKey, "join-password", "line one\nline two", EGameServerSecretOrigin.UserProvided, seed.User.PrimaryKey));

        var revealed = await service.RevealAsync(seed.Server.PrimaryKey, "join-password", seed.User.PrimaryKey);
        Assert.Equal("two words", revealed.Value);
        Assert.DoesNotContain("line one", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies that the status projection carries the chosen-value acceptance and the value format.</summary>
    [Fact]
    public async Task GetStatusesAsync_ProjectsChosenValueAcceptanceAndFormat()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);

        var statuses = (await service.GetStatusesAsync(seed.Server.PrimaryKey)).ToDictionary(q => q.Key);

        Assert.True(statuses["rcon-password"].AcceptsChosenValue);
        Assert.Equal(EGameSecretValueFormat.Token, statuses["rcon-password"].ValueFormat);
        Assert.True(statuses["gslt"].AcceptsChosenValue);
        Assert.Equal(EGameSecretValueFormat.SingleLine, statuses["gslt"].ValueFormat);
        Assert.True(statuses["join-password"].AcceptsChosenValue);
        Assert.Equal(EGameSecretValueFormat.SingleLine, statuses["join-password"].ValueFormat);
    }

    /// <summary>Verifies the chosen-value declarations of the Arma 3 credentials.</summary>
    [Fact]
    public void Arma3Definition_DeclaresChosenValueAcceptanceAndFormats()
    {
        var secrets = new Arma3GameServerDefinitionRegistration().Definition.Secrets.ToDictionary(q => q.Key);

        Assert.True(secrets["admin-password"].AcceptsChosenValue);
        Assert.Equal(EGameSecretValueFormat.SingleLine, secrets["admin-password"].ValueFormat);
        Assert.True(secrets["server-command-password"].AcceptsChosenValue);
        Assert.Equal(EGameSecretValueFormat.SingleLine, secrets["server-command-password"].ValueFormat);
        Assert.True(secrets["battleye-rcon-password"].AcceptsChosenValue);
        Assert.Equal(EGameSecretValueFormat.Token, secrets["battleye-rcon-password"].ValueFormat);
        Assert.False(secrets["join-password"].Generated);
    }

    /// <summary>Verifies that setting an external secret records the credentials area as pending with a timestamp.</summary>
    [Fact]
    public async Task SetAsync_MarksCredentialsPending()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);

        await service.SetAsync(seed.Server.PrimaryKey, "gslt", "external-token", EGameServerSecretOrigin.ExternalProvider, seed.User.PrimaryKey);

        var server = await store.DbContext.GameServers.AsNoTracking().SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Credentials, server.PendingStartChangeAreas);
        Assert.NotNull(server.PendingStartSince);
    }

    /// <summary>Verifies that a rotation adds the credentials area and keeps an earlier pending timestamp.</summary>
    [Fact]
    public async Task RotateAsync_MarksCredentialsAndKeepsEarlierPendingSince()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: false);
        var earlier = Instant.FromUnixTimeSeconds(1_600_000_000);
        var tracked = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        tracked.PendingStartChangeAreas = EGameServerChangeArea.Configuration;
        tracked.PendingStartSince = earlier;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();

        await service.RotateAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);

        var server = await store.DbContext.GameServers.AsNoTracking().SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Configuration | EGameServerChangeArea.Credentials, server.PendingStartChangeAreas);
        Assert.Equal(earlier, server.PendingStartSince);
    }

    /// <summary>Verifies that archived servers reject setting and rotating before any version or mark changes.</summary>
    [Fact]
    public async Task SetAndRotate_OnArchivedServer_ThrowConflictAndChangeNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: true);
        var versionsBefore = await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey);

        await Assert.ThrowsAsync<HostingConflictException>(
            () => service.SetAsync(seed.Server.PrimaryKey, "gslt", "external-token", EGameServerSecretOrigin.ExternalProvider, seed.User.PrimaryKey));
        await Assert.ThrowsAsync<HostingConflictException>(
            () => service.RotateAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey));

        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.AsNoTracking().SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.None, server.PendingStartChangeAreas);
        Assert.Null(server.PendingStartSince);
        Assert.Equal(versionsBefore, await store.DbContext.GameServerSecrets.CountAsync(q => q.GameServerFk == seed.Server.PrimaryKey));
    }

    /// <summary>Verifies that revealing stays available and audited on an archived server.</summary>
    [Fact]
    public async Task RevealAsync_OnArchivedServer_ReturnsValueAndAudits()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, service) = await PrepareCounterStrike2Async(store, archived: true);

        var revealed = await service.RevealAsync(seed.Server.PrimaryKey, "rcon-password", seed.User.PrimaryKey);

        Assert.Equal(64, revealed.Value.Length);
        Assert.True(await store.DbContext.HostingAuditRecords.AnyAsync(
            q => q.GameServerFk == seed.Server.PrimaryKey && q.Kind == EHostingAuditEventKind.ServerSecretRevealed));
    }

    /// <summary>
    /// Seeds a Counter-Strike 2 server with its generated secrets and builds the service under test.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="archived">Whether the seeded server is archived.</param>
    /// <returns>The seed and the service.</returns>
    private static async Task<(HostingTestSeed Seed, GameServerSecretService Service)> PrepareCounterStrike2Async(
        HostingTestStore store,
        bool archived)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.Include(q => q.Secrets).SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        server.GameIdentifier = CounterStrike2GameServerDefinitionRegistration.GameIdentifier;
        var definition = new CounterStrike2GameServerDefinitionRegistration().Definition;
        var service = Create(store, definition);
        service.EnsureGeneratedSecrets(server, definition, seed.User.PrimaryKey, NodaSystemClock.Instance.GetCurrentInstant());
        server.IsArchived = archived;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        return (seed, service);
    }

    private static GameServerSecretService Create(HostingTestStore store, GameServerDefinition definition)
        => new(
            store.DbContext,
            new GameServerDefinitionCatalog([new Registration(definition)]),
            new EphemeralDataProtectionProvider(),
            new HostingAuditWriter());

    private sealed record Registration(GameServerDefinition Definition) : IGameServerDefinitionRegistration;
}
