using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;
using Claim = System.Security.Claims.Claim;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerCreationCostOwnerOptionsTests
{
    [Fact]
    public async Task CreatorOnlyReceivesPersonalAndExplicitlyUsableOwners()
    {
        var userId = Guid.NewGuid();
        var ownOwnerId = Guid.NewGuid();
        var grantedOwnerId = Guid.NewGuid();
        var hiddenOwnerId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        await using var store = await HostingTestStore.CreateAsync();
        store.DbContext.Users.Add(new User { PrimaryKey = userId, Nickname = "Creator" });
        store.DbContext.CostOwners.AddRange(
            Owner(ownOwnerId, "Personal", userId),
            Owner(grantedOwnerId, "Shared", null),
            Owner(hiddenOwnerId, "Hidden", null));
        await store.DbContext.SaveChangesAsync();

        var principal = Principal(
            userId,
            new Claim(Claims.ResourceBased.CostOwner.Use, grantedOwnerId.ToString()),
            new Claim(Claims.ResourceBased.CostOwner.Use, hiddenOwnerId.ToString().ToUpperInvariant()),
            new Claim(Claims.ResourceBased.CostOwner.Use, "not-a-guid"));
        var service = CreateService(store.DbContext);

        var options = await service.GetCreationCostOwnerOptionsAsync(principal);

        Assert.Equal([ownOwnerId, grantedOwnerId], options.Select(q => q.CostOwnerId));
        Assert.True(options.First().IsCurrentUser);
        Assert.DoesNotContain(options, q => q.CostOwnerId == hiddenOwnerId);
    }

    [Fact]
    public async Task HostingAdministratorReceivesEveryOwnerAndFrozenState()
    {
        var userId = Guid.NewGuid();
        var ownOwnerId = Guid.NewGuid();
        var frozenOwnerId = Guid.NewGuid();
        await using var store = await HostingTestStore.CreateAsync();
        store.DbContext.Users.Add(new User { PrimaryKey = userId, Nickname = "Administrator" });
        store.DbContext.CostOwners.AddRange(
            Owner(ownOwnerId, "Personal", userId),
            Owner(frozenOwnerId, "Verein", null, true));
        await store.DbContext.SaveChangesAsync();

        var principal = Principal(userId, new Claim(Claims.Administrative.Hosting, string.Empty));
        var options = await CreateService(store.DbContext).GetCreationCostOwnerOptionsAsync(principal);

        Assert.Equal(2, options.Count);
        Assert.True(options.Single(q => q.CostOwnerId == frozenOwnerId).IsFrozen);
    }

    /// <summary>The creation options report the kind of every owner and change no other field.</summary>
    /// <returns>A task completing after the kinds are verified.</returns>
    [Fact]
    public async Task CreationOptions_CarryTheKindOfEveryOwner()
    {
        var userId = Guid.NewGuid();
        var ownOwnerId = Guid.NewGuid();
        var verein = Owner(Guid.NewGuid(), "Verein", null);
        verein.Kind = ECostOwnerKind.Verein;
        var external = Owner(Guid.NewGuid(), "External", null);
        await using var store = await HostingTestStore.CreateAsync();
        store.DbContext.Users.Add(new User { PrimaryKey = userId, Nickname = "Administrator" });
        store.DbContext.CostOwners.AddRange(Owner(ownOwnerId, "Personal", userId), verein, external);
        await store.DbContext.SaveChangesAsync();

        var options = await CreateService(store.DbContext)
            .GetCreationCostOwnerOptionsAsync(Principal(userId, new Claim(Claims.Administrative.Hosting, string.Empty)));

        Assert.Equal(
            [
                (ownOwnerId, ECostOwnerKind.User, true),
                (external.PrimaryKey, ECostOwnerKind.ExternalOrganization, false),
                (verein.PrimaryKey, ECostOwnerKind.Verein, false),
            ],
            options.Select(q => (q.CostOwnerId, q.Kind, q.IsCurrentUser)));
    }

    /// <summary>A hosting administrator lists every owner but the current one, frozen ones flagged, with the kind.</summary>
    /// <returns>A task completing after the administrator listing is verified.</returns>
    [Fact]
    public async Task TransferOptions_ForAdministrator_ListEveryOwnerExceptTheCurrentOne()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var frozen = Owner(Guid.NewGuid(), "Frozen Verein", null, true);
        frozen.Kind = ECostOwnerKind.Verein;
        var external = Owner(Guid.NewGuid(), "External", null);
        store.DbContext.CostOwners.AddRange(frozen, external);
        await store.DbContext.SaveChangesAsync();
        var administrator = HostingTestData.Principal(
            seed.User,
            (Claims.Administrative.Hosting, string.Empty),
            (Claims.ResourceBased.Server.Modify, seed.Server.PrimaryKey.ToString()));

        var options = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, 20, administrator);

        Assert.DoesNotContain(options, q => q.CostOwnerId == seed.CostOwner.PrimaryKey);
        Assert.Equal([external.PrimaryKey, frozen.PrimaryKey], options.Select(q => q.CostOwnerId));
        var frozenOption = options.Single(q => q.CostOwnerId == frozen.PrimaryKey);
        Assert.True(frozenOption.IsFrozen);
        Assert.Equal(ECostOwnerKind.Verein, frozenOption.Kind);
        Assert.False(options.Single(q => q.CostOwnerId == external.PrimaryKey).IsFrozen);
    }

    /// <summary>A Modify holder gets their own owner first and the owners with an exact use grant, nothing else.</summary>
    /// <returns>A task completing after the member listing is verified.</returns>
    [Fact]
    public async Task TransferOptions_ForModifyMember_ListOwnOwnerFirstThenExactGrants()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var memberId = Guid.NewGuid();
        var member = new User { PrimaryKey = memberId, Nickname = "Member" };
        var ownOwner = Owner(Guid.NewGuid(), "Zeta personal", memberId);
        var grantedOwner = Owner(Guid.NewGuid(), "Alpha shared", null);
        var hiddenOwner = Owner(Guid.NewGuid(), "Hidden", null);
        store.DbContext.Users.Add(member);
        store.DbContext.CostOwners.AddRange(ownOwner, grantedOwner, hiddenOwner);
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.Principal(
            member,
            (Claims.ResourceBased.Server.Modify, seed.Server.PrimaryKey.ToString()),
            (Claims.ResourceBased.CostOwner.Use, grantedOwner.PrimaryKey.ToString()),
            (Claims.ResourceBased.CostOwner.Use, seed.CostOwner.PrimaryKey.ToString()));

        var options = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, 20, principal);

        Assert.Equal([ownOwner.PrimaryKey, grantedOwner.PrimaryKey], options.Select(q => q.CostOwnerId));
        Assert.True(options.First().IsCurrentUser);
        Assert.DoesNotContain(options, q => q.CostOwnerId == hiddenOwner.PrimaryKey);
        Assert.DoesNotContain(options, q => q.CostOwnerId == seed.CostOwner.PrimaryKey);
    }

    /// <summary>The caller's own owner is not offered when it already pays for the server.</summary>
    /// <returns>A task completing after the exclusion is verified.</returns>
    [Fact]
    public async Task TransferOptions_ExcludeTheCurrentOwnerEvenWhenItIsTheCallersOwn()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify);

        var options = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, 20, principal);

        Assert.Empty(options);
    }

    /// <summary>The take bound limits the result after the ordering is applied.</summary>
    /// <returns>A task completing after the limit is verified.</returns>
    [Fact]
    public async Task TransferOptions_HonorTheTakeLimitAfterOrdering()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var owners = new[] { Owner(Guid.NewGuid(), "C", null), Owner(Guid.NewGuid(), "A", null), Owner(Guid.NewGuid(), "B", null) };
        store.DbContext.CostOwners.AddRange(owners);
        await store.DbContext.SaveChangesAsync();
        var administrator = HostingTestData.Principal(
            seed.User,
            (Claims.Administrative.Hosting, string.Empty));

        var options = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, 2, administrator);

        Assert.Equal(["A", "B"], options.Select(q => q.DisplayName));
    }

    /// <summary>A caller without the Modify permission on the server is refused before any lookup.</summary>
    /// <returns>A task completing after the refusal is verified.</returns>
    [Fact]
    public async Task TransferOptions_WithoutModify_ThrowForbidden()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => HostingTestData
            .CreateProjectionService(store.DbContext)
            .GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, 20, principal));
    }

    /// <summary>An unknown server and an archived server both answer not found.</summary>
    /// <returns>A task completing after both lookups are verified.</returns>
    [Fact]
    public async Task TransferOptions_ForUnknownOrArchivedServer_ThrowNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var unknownId = Guid.NewGuid();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var unknownPrincipal = HostingTestData.Principal(
            seed.User,
            (Claims.ResourceBased.Server.Modify, unknownId.ToString()));

        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => projection.GetCostOwnerOptionsAsync(unknownId, null, 20, unknownPrincipal));

        seed.Server.IsArchived = true;
        await store.DbContext.SaveChangesAsync();
        var archivedPrincipal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Modify);
        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => projection.GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, 20, archivedPrincipal));
    }

    /// <summary>A take outside 1 to 50 is rejected by the service for every caller.</summary>
    /// <param name="take">The invalid limit.</param>
    /// <returns>A task completing after the validation is verified.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task TransferOptions_WithTakeOutsideBounds_ThrowValidation(int take)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify);

        await Assert.ThrowsAsync<HostingValidationException>(() => HostingTestData
            .CreateProjectionService(store.DbContext)
            .GetCostOwnerOptionsAsync(seed.Server.PrimaryKey, null, take, principal));
    }

    /// <summary>
    /// The display-name search translates for Npgsql to a case-insensitive ILIKE that carries the backslash escape
    /// clause and the escaped pattern, proven offline with <c>ToQueryString</c> on a metadata-only context.
    /// </summary>
    [Fact]
    public void ApplyDisplayNameSearch_TranslatesForNpgsqlToEscapedIlike()
    {
        using var dbContext = CreateNpgsqlModelDbContext();

        var sql = GameServerProjectionService
            .ApplyDisplayNameSearch(dbContext.CostOwners.AsNoTracking(), " 50%_a\\b ")
            .ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.Ordinal);
        Assert.Contains("ESCAPE", sql, StringComparison.Ordinal);
        Assert.Contains("%50\\%\\_a\\\\b%", sql, StringComparison.Ordinal);
    }

    /// <summary>A blank search adds no filter.</summary>
    /// <param name="search">The blank search text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyDisplayNameSearch_WithBlankSearch_AddsNoFilter(string? search)
    {
        using var dbContext = CreateNpgsqlModelDbContext();

        var sql = GameServerProjectionService
            .ApplyDisplayNameSearch(dbContext.CostOwners.AsNoTracking(), search)
            .ToQueryString();

        Assert.DoesNotContain("ILIKE", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a context with the Npgsql provider and the application model that is only used for query
    /// translation and never opens a connection.
    /// </summary>
    /// <returns>The metadata-only Npgsql context.</returns>
    private static DatabaseContext CreateNpgsqlModelDbContext()
    {
        var options = new DbContextOptionsBuilder<NpgsqlModelDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=metadata-only;Username=metadata;Password=metadata",
                npgsqlOptions => npgsqlOptions.UseNodaTime())
            .Options;
        return new NpgsqlModelDbContext(options);
    }

    private static GameServerProjectionService CreateService(DatabaseContext dbContext)
        => new(
            dbContext,
            new EmptyGameServerDefinitionCatalog(),
            [],
            null!,
            null!,
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            Options.Create(new StorageDemandOptions()),
            NullLogger<GameServerProjectionService>.Instance,
            new GameServerConfigurationService(dbContext),
            Options.Create(new HostingFinanceOptions()),
            Api.Tests.ServerAutomation.ServerAutomationTestData.CreateProjector(dbContext));

    private static CostOwner Owner(Guid id, string displayName, Guid? userId, bool frozen = false)
        => new()
        {
            PrimaryKey = id,
            DisplayName = displayName,
            UserFk = userId,
            Kind = userId.HasValue ? ECostOwnerKind.User : ECostOwnerKind.ExternalOrganization,
            IsFrozen = frozen,
            Currency = "EUR",
        };

    private static ClaimsPrincipal Principal(Guid userId, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new Claim(ClaimTypes.Name, userId.ToString()));
        identity.AddClaims(claims);
        return new ClaimsPrincipal(identity);
    }

    private sealed class EmptyGameServerDefinitionCatalog : IGameServerDefinitionCatalog
    {
        public IReadOnlyList<GameServerDefinition> GetDefinitions() => [];
    }

    /// <summary>
    /// The application database context bound to Npgsql options, used only for offline query translation.
    /// </summary>
    /// <param name="dbContextOptions">The Npgsql options of the context.</param>
    private sealed class NpgsqlModelDbContext(DbContextOptions<NpgsqlModelDbContext> dbContextOptions)
        : DatabaseContext(dbContextOptions);
}
