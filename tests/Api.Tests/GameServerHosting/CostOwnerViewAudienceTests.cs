using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the cost-owner view audience equals <see cref="IHostingAuthorizationService.CanViewCostOwnerAsync"/>
/// for every claim class, excludes banned and deleted users, resolves many cost owners per call, ignores unrequested
/// claim values and translates for Npgsql.
/// </summary>
public sealed class CostOwnerViewAudienceTests
{
    /// <summary>
    /// An empty or null valued global, server or hosting administrative claim, held directly or through a role, lists
    /// the user for every requested cost owner.
    /// </summary>
    /// <param name="identifier">The administrative claim identifier.</param>
    /// <param name="value">The stored claim value; <see langword="null"/> counts as empty.</param>
    /// <param name="throughRole">Whether the claim is granted through a role instead of directly.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.Administrative.All, "", false)]
    [InlineData(Claims.Administrative.All, null, false)]
    [InlineData(Claims.Administrative.Server, "", false)]
    [InlineData(Claims.Administrative.Server, null, false)]
    [InlineData(Claims.Administrative.Hosting, "", false)]
    [InlineData(Claims.Administrative.Hosting, null, false)]
    [InlineData(Claims.Administrative.All, "", true)]
    [InlineData(Claims.Administrative.Server, null, true)]
    [InlineData(Claims.Administrative.Hosting, "", true)]
    public async Task EmptyAdministrativeClaim_ListsUserForEveryOwner(
        string identifier,
        string? value,
        bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var owners = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var user = CreateUser("administrator");
        var claim = await claims.GetAsync(identifier, value);
        if (throughRole)
            user.Roles!.Add(CreateRole("administrators", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync(owners);

        Assert.All(owners, owner => Assert.Equal([user.PrimaryKey], audience[owner]));
    }

    /// <summary>
    /// An empty or null valued fin or fin:view-all claim, held directly or through a role, lists the user for every
    /// requested cost owner.
    /// </summary>
    /// <param name="identifier">The finance claim identifier.</param>
    /// <param name="value">The stored claim value; <see langword="null"/> counts as empty.</param>
    /// <param name="throughRole">Whether the claim is granted through a role instead of directly.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.Finance.All, "", false)]
    [InlineData(Claims.Finance.All, null, false)]
    [InlineData(Claims.Finance.ViewAll, "", false)]
    [InlineData(Claims.Finance.ViewAll, null, false)]
    [InlineData(Claims.Finance.All, null, true)]
    [InlineData(Claims.Finance.ViewAll, "", true)]
    [InlineData(Claims.Finance.ViewAll, null, true)]
    public async Task EmptyFinanceClaim_ListsUserForEveryOwner(
        string identifier,
        string? value,
        bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var owners = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var user = CreateUser("finance");
        var claim = await claims.GetAsync(identifier, value);
        if (throughRole)
            user.Roles!.Add(CreateRole("finance", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync(owners);

        Assert.All(owners, owner => Assert.Equal([user.PrimaryKey], audience[owner]));
    }

    /// <summary>
    /// A non-empty valued finance claim and unrelated finance claims never list a user.
    /// </summary>
    /// <param name="identifier">The claim identifier granted.</param>
    /// <param name="value">The stored claim value.</param>
    /// <param name="throughRole">Whether the claim is granted through a role instead of directly.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.Finance.ViewAll, "not-empty", false)]
    [InlineData(Claims.Finance.All, "not-empty", false)]
    [InlineData(Claims.Finance.ManagePolicy, "", false)]
    [InlineData(Claims.Finance.RecordPayment, null, false)]
    [InlineData(Claims.Finance.ViewAll, "not-empty", true)]
    [InlineData(Claims.Finance.ManagePolicy, "", true)]
    public async Task NonViewingFinanceClaim_NeverListsUser(string identifier, string? value, bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var owner = Guid.NewGuid();
        var user = CreateUser("not-a-viewer");
        var claim = await claims.GetAsync(identifier, value);
        if (throughRole)
            user.Roles!.Add(CreateRole("not-viewers", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync([owner]);

        Assert.Empty(audience[owner]);
    }

    /// <summary>
    /// A user who holds the cost-owner view claim valued with one owner id, directly or through a role, is listed
    /// for that owner only.
    /// </summary>
    /// <param name="throughRole">Whether the claim is granted through a role instead of directly.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CostOwnerViewClaim_ListsUserForThatOwnerOnly(bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var user = CreateUser("co-viewer");
        var claim = await claims.GetAsync(Claims.ResourceBased.CostOwner.View, owner.ToString());
        if (throughRole)
            user.Roles!.Add(CreateRole("co-viewers", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync([owner, other]);

        Assert.Equal([user.PrimaryKey], audience[owner]);
        Assert.Empty(audience[other]);
    }

    /// <summary>
    /// The use claim alone, a valueless view claim, another owner's view claim and a differently cased id value never
    /// list a user.
    /// </summary>
    /// <param name="identifier">The claim identifier granted.</param>
    /// <param name="valueKind">
    /// Which value is stored: <c>null</c>, <c>owner</c> (the owner id), <c>upper</c> (the owner id in upper-case hex)
    /// or <c>other</c> (another owner's id).
    /// </param>
    /// <param name="throughRole">Whether the claim is granted through a role instead of directly.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.CostOwner.Use, "owner", false)]
    [InlineData(Claims.ResourceBased.CostOwner.View, "null", false)]
    [InlineData(Claims.ResourceBased.CostOwner.View, "other", false)]
    [InlineData(Claims.ResourceBased.CostOwner.View, "upper", false)]
    [InlineData(Claims.ResourceBased.CostOwner.Use, "owner", true)]
    [InlineData(Claims.ResourceBased.CostOwner.View, "other", true)]
    public async Task NonViewingOwnerClaim_NeverListsUser(string identifier, string valueKind, bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var owner = Guid.NewGuid();
        var value = valueKind switch
        {
            "null"  => null,
            "owner" => owner.ToString(),
            "upper" => owner.ToString().ToUpperInvariant(),
            "other" => Guid.NewGuid().ToString(),
            _       => throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, null),
        };
        var user = CreateUser("not-a-viewer");
        var claim = await claims.GetAsync(identifier, value);
        if (throughRole)
            user.Roles!.Add(CreateRole("not-viewers", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync([owner]);

        Assert.Empty(audience[owner]);
    }

    /// <summary>
    /// The user behind a personal cost owner is listed for that owner and for no other owner.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task OwnersOwnUser_IsListedForOwnOwnerOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var user = CreateUser("owner");
        store.DbContext.Users.Add(user);
        var personal = CreateOwner("personal", user);
        var external = CreateOwner("external", null);
        store.DbContext.CostOwners.AddRange(personal, external);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store)
            .GetCostOwnerViewAudienceAsync([personal.PrimaryKey, external.PrimaryKey]);

        Assert.Equal([user.PrimaryKey], audience[personal.PrimaryKey]);
        Assert.Empty(audience[external.PrimaryKey]);
    }

    /// <summary>
    /// Banned and deleted users are listed for no cost owner, even with otherwise sufficient claims or as the owner's
    /// own user.
    /// </summary>
    /// <param name="banned">Whether the user is banned.</param>
    /// <param name="deleted">Whether the user is deleted.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BannedOrDeletedUser_IsInNoAudience(bool banned, bool deleted)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var user = CreateUser("excluded");
        user.IsBanned = banned;
        user.IsDeleted = deleted;
        store.DbContext.Users.Add(user);
        var owner = CreateOwner("personal", user);
        store.DbContext.CostOwners.Add(owner);
        user.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.CostOwner.View, owner.PrimaryKey.ToString()));
        user.Claims.Add(await claims.GetAsync(Claims.Finance.All, string.Empty));
        user.Claims.Add(await claims.GetAsync(Claims.Administrative.Hosting, string.Empty));
        user.Roles!.Add(CreateRole(
            "viewers",
            await claims.GetAsync(Claims.ResourceBased.CostOwner.View, owner.PrimaryKey.ToString())));
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync([owner.PrimaryKey]);

        Assert.Empty(audience[owner.PrimaryKey]);
    }

    /// <summary>
    /// One call resolves several owners: every requested id has an entry, an owner nobody may view maps to an empty
    /// collection and a user matching through both a user claim and a role is listed once.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task ManyOwners_ResolveInOneCallWithoutDuplicates()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var unviewable = Guid.NewGuid();
        var both = CreateUser("both");
        var viewClaim = await claims.GetAsync(Claims.ResourceBased.CostOwner.View, first.ToString());
        both.Claims!.Add(viewClaim);
        both.Roles!.Add(CreateRole("first-viewers", viewClaim));
        var secondOnly = CreateUser("second-only");
        secondOnly.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.CostOwner.View, second.ToString()));
        store.DbContext.Users.AddRange(both, secondOnly);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync([first, second, unviewable, first]);

        Assert.Equal(3, audience.Count);
        Assert.Equal([both.PrimaryKey], audience[first]);
        Assert.Equal([secondOnly.PrimaryKey], audience[second]);
        Assert.Empty(audience[unviewable]);
    }

    /// <summary>
    /// An empty id list yields an empty dictionary.
    /// </summary>
    /// <returns>A task completing once the assertion ran.</returns>
    [Fact]
    public async Task EmptyIdList_ReturnsEmptyDictionary()
    {
        await using var store = await HostingTestStore.CreateAsync();

        var audience = await CreateService(store).GetCostOwnerViewAudienceAsync([]);

        Assert.Empty(audience);
    }

    /// <summary>
    /// For every row of a claim matrix, audience membership equals
    /// <see cref="IHostingAuthorizationService.CanViewCostOwnerAsync"/> evaluated on the principal
    /// <see cref="IHostingAuthorizationService.RebuildPrincipalAsync"/> builds, where a missing principal means not
    /// visible.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Audience_MatchesCanViewCostOwnerForEveryMatrixRow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var owner = CreateOwner("A", null);
        var other = CreateOwner("B", null);
        store.DbContext.CostOwners.AddRange(owner, other);
        var a = owner.PrimaryKey.ToString();
        var b = other.PrimaryKey.ToString();
        var rows = new MatrixRow[]
        {
            new("no claims", [], [], false, false, false),
            new("adm empty", [(Claims.Administrative.All, string.Empty)], [], false, false, false),
            new("adm null", [(Claims.Administrative.All, null)], [], false, false, false),
            new("adm:sv empty", [(Claims.Administrative.Server, string.Empty)], [], false, false, false),
            new("adm:hosting null", [(Claims.Administrative.Hosting, null)], [], false, false, false),
            new("role adm:hosting empty", [], [(Claims.Administrative.Hosting, string.Empty)], false, false, false),
            new("role adm null", [], [(Claims.Administrative.All, null)], false, false, false),
            new("adm non-empty", [(Claims.Administrative.All, "x")], [], false, false, false),
            new("adm:ev empty", [(Claims.Administrative.Event, string.Empty)], [], false, false, false),
            new("fin empty", [(Claims.Finance.All, string.Empty)], [], false, false, false),
            new("fin null", [(Claims.Finance.All, null)], [], false, false, false),
            new("fin:view-all empty", [(Claims.Finance.ViewAll, string.Empty)], [], false, false, false),
            new("fin:view-all null", [(Claims.Finance.ViewAll, null)], [], false, false, false),
            new("role fin empty", [], [(Claims.Finance.All, string.Empty)], false, false, false),
            new("role fin:view-all null", [], [(Claims.Finance.ViewAll, null)], false, false, false),
            new("mixed-case FIN:View-All empty", [("FIN:View-All", string.Empty)], [], false, false, false),
            new("fin non-empty", [(Claims.Finance.All, "x")], [], false, false, false),
            new("fin:view-all non-empty", [(Claims.Finance.ViewAll, a)], [], false, false, false),
            new("fin:manage-policy empty", [(Claims.Finance.ManagePolicy, string.Empty)], [], false, false, false),
            new("fin:record-payment null", [(Claims.Finance.RecordPayment, null)], [], false, false, false),
            new("co:view A", [(Claims.ResourceBased.CostOwner.View, a)], [], false, false, false),
            new("role co:view A", [], [(Claims.ResourceBased.CostOwner.View, a)], false, false, false),
            new("user and role co:view A", [(Claims.ResourceBased.CostOwner.View, a)],
                [(Claims.ResourceBased.CostOwner.View, a)], false, false, false),
            new("co:view B", [(Claims.ResourceBased.CostOwner.View, b)], [], false, false, false),
            new("co:use A", [(Claims.ResourceBased.CostOwner.Use, a)], [], false, false, false),
            new("role co:use A", [], [(Claims.ResourceBased.CostOwner.Use, a)], false, false, false),
            new("bare co:view", [(Claims.ResourceBased.CostOwner.View, null)], [], false, false, false),
            new("co:view upper-case A", [(Claims.ResourceBased.CostOwner.View, a.ToUpperInvariant())], [],
                false, false, false),
            new("mixed-case CO:View A", [("CO:View", a)], [], false, false, false),
            new("role mixed-case Co:View A", [], [("Co:View", a)], false, false, false),
            new("owner's own user", [], [], false, false, true),
            new("banned owner's own user", [], [], true, false, true),
            new("deleted owner's own user", [], [], false, true, true),
            new("owner's own user with co:view B", [(Claims.ResourceBased.CostOwner.View, b)], [],
                false, false, true),
            new("banned fin empty", [(Claims.Finance.All, string.Empty)], [], true, false, false),
            new("deleted co:view A", [(Claims.ResourceBased.CostOwner.View, a)], [], false, true, false),
            new("banned role adm:sv empty", [], [(Claims.Administrative.Server, string.Empty)], true, false, false),
        };
        var users = new List<(MatrixRow Row, User User, CostOwner? Personal)>();
        foreach (var row in rows)
        {
            var user = CreateUser(row.Name);
            user.IsBanned = row.Banned;
            user.IsDeleted = row.Deleted;
            foreach (var (identifier, value) in row.UserClaims)
                user.Claims!.Add(await claims.GetAsync(identifier, value));
            if (row.RoleClaims.Count > 0)
            {
                var roleClaims = new List<Claim>();
                foreach (var (identifier, value) in row.RoleClaims)
                    roleClaims.Add(await claims.GetAsync(identifier, value));
                user.Roles!.Add(CreateRole(row.Name, roleClaims.ToArray()));
            }

            store.DbContext.Users.Add(user);
            var personal = row.OwnsPersonalOwner ? CreateOwner(row.Name, user) : null;
            if (personal is not null)
                store.DbContext.CostOwners.Add(personal);
            users.Add((row, user, personal));
        }

        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var requested = new List<Guid> { owner.PrimaryKey, other.PrimaryKey };
        requested.AddRange(users.Where(q => q.Personal is not null).Select(q => q.Personal!.PrimaryKey));

        var audience = await service.GetCostOwnerViewAudienceAsync(requested);

        foreach (var (row, user, _) in users)
        {
            var principal = await service.RebuildPrincipalAsync(user.PrimaryKey);
            foreach (var ownerId in requested)
            {
                var expected = principal is not null
                               && await service.CanViewCostOwnerAsync(principal, ownerId);
                var actual = audience[ownerId].Contains(user.PrimaryKey);
                Assert.True(
                    expected == actual,
                    $"Row '{row.Name}' on owner {ownerId}: CanViewCostOwnerAsync={expected}, audience={actual}.");
            }
        }

        Assert.All(
            audience.Values,
            entry => Assert.Equal(entry.Count, entry.Distinct().Count()));
    }

    /// <summary>
    /// The cost-owner audience query builders translate for Npgsql and filter on the banned and deleted columns,
    /// proven offline with <c>ToQueryString</c> on a metadata-only Npgsql context that never connects.
    /// </summary>
    [Fact]
    public void AudienceQueries_TranslateForNpgsql()
    {
        using var dbContext = CreateNpgsqlModelDbContext();
        var users = dbContext.Users.AsNoTracking();
        string[] ownerValues = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];

        var queries = new[]
        {
            HostingAuthorizationService.BuildFinanceViewerQuery(users).ToQueryString(),
            HostingAuthorizationService.BuildDirectCostOwnerViewGrantQuery(users, ownerValues).ToQueryString(),
            HostingAuthorizationService.BuildRoleCostOwnerViewGrantQuery(users, ownerValues).ToQueryString(),
        };

        Assert.All(queries, sql =>
        {
            Assert.Contains("\"IsBanned\"", sql, StringComparison.Ordinal);
            Assert.Contains("\"IsDeleted\"", sql, StringComparison.Ordinal);
            Assert.Contains("lower(", sql, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Creates the authorization service under test on the store's database context.
    /// </summary>
    /// <param name="store">The test store whose context the service reads.</param>
    /// <returns>The service under test.</returns>
    private static HostingAuthorizationService CreateService(HostingTestStore store)
        => new(store.DbContext);

    /// <summary>
    /// Creates an active, unsaved user with empty claim and role collections.
    /// </summary>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>The created user.</returns>
    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = nickname,
            AvatarMimeType = string.Empty,
            Claims = [],
            Roles = [],
            Discord = new DiscordUser { Username = nickname },
            Steam = new SteamUser(),
        };

    /// <summary>
    /// Creates an unsaved role carrying the given claims.
    /// </summary>
    /// <param name="title">The title of the role.</param>
    /// <param name="claims">The claims the role grants.</param>
    /// <returns>The created role.</returns>
    private static Role CreateRole(string title, params Claim[] claims)
        => new()
        {
            Title = title,
            Description = "test role",
            Claims = claims.ToList(),
        };

    /// <summary>
    /// Creates an unsaved cost owner.
    /// </summary>
    /// <param name="displayName">The display name of the cost owner.</param>
    /// <param name="user">The user a personal cost owner belongs to, or <see langword="null"/> for an external
    /// organization.</param>
    /// <returns>The created cost owner.</returns>
    private static CostOwner CreateOwner(string displayName, User? user)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = user is null ? ECostOwnerKind.ExternalOrganization : ECostOwnerKind.User,
            User = user,
            UserFk = user?.PrimaryKey,
            DisplayName = displayName,
            Currency = "EUR",
            TimeStampCreated = Instant.FromUnixTimeSeconds(1_700_000_000),
            TimeStampUpdated = Instant.FromUnixTimeSeconds(1_700_000_000),
        };

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

    /// <summary>
    /// One row of the drift matrix: the claims a user holds directly and through one role, the user's flags and
    /// whether the user is the user behind a personal cost owner.
    /// </summary>
    /// <param name="Name">The row name used in assertion messages and as the user's nickname.</param>
    /// <param name="UserClaims">The claims granted directly to the user as identifier and stored value.</param>
    /// <param name="RoleClaims">The claims granted through one role as identifier and stored value.</param>
    /// <param name="Banned">Whether the user is banned.</param>
    /// <param name="Deleted">Whether the user is deleted.</param>
    /// <param name="OwnsPersonalOwner">Whether a personal cost owner of this user is part of the request.</param>
    private sealed record MatrixRow(
        string Name,
        IReadOnlyList<(string Identifier, string? Value)> UserClaims,
        IReadOnlyList<(string Identifier, string? Value)> RoleClaims,
        bool Banned,
        bool Deleted,
        bool OwnsPersonalOwner);

    /// <summary>
    /// Hands out one shared claim row per identifier and value within a store, reusing seeded catalog rows, because
    /// duplicate rows violate the claim unique indexes.
    /// </summary>
    /// <param name="dbContext">The store's database context the rows are read from and added to.</param>
    private sealed class ClaimRows(DatabaseContext dbContext)
    {
        /// <summary>
        /// The rows handed out so far, keyed by identifier and value (with <c>\0</c> standing for a null value).
        /// </summary>
        private readonly Dictionary<(string Identifier, string Value), Claim> _rows = new();

        /// <summary>
        /// Returns the claim row for an identifier and value, finding a stored row or creating and adding a new one.
        /// </summary>
        /// <param name="identifier">The claim identifier, compared exactly.</param>
        /// <param name="value">The stored claim value, or <see langword="null"/> for a valueless claim.</param>
        /// <returns>A task resolving to the shared claim row.</returns>
        public async Task<Claim> GetAsync(string identifier, string? value)
        {
            var key = (identifier, value ?? "\0");
            if (_rows.TryGetValue(key, out var cached))
                return cached;
            var stored = await dbContext.Claims.SingleOrDefaultAsync(q =>
                q.Identifier == identifier && q.Value == value && q.ValueType == null);
            var claim = stored ?? new Claim
            {
                Identifier = identifier,
                Value = value,
                Category = "test",
                Title = identifier,
                Description = "test claim",
            };
            if (stored is null)
                dbContext.Claims.Add(claim);
            _rows[key] = claim;
            return claim;
        }
    }

    /// <summary>
    /// The application database context bound to Npgsql options, used only for offline query translation.
    /// </summary>
    /// <param name="dbContextOptions">The Npgsql options of the context.</param>
    private sealed class NpgsqlModelDbContext(DbContextOptions<NpgsqlModelDbContext> dbContextOptions)
        : DatabaseContext(dbContextOptions);
}
