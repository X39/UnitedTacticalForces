using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the server view audience equals the REST fleet view rule for every claim class, excludes banned and
/// deleted users, resolves many servers per call, reads current claims and translates for Npgsql.
/// </summary>
public sealed class HostingFleetAudienceTests
{
    /// <summary>
    /// A user who directly holds the view or all-actions claim bound to one server sees that server and no other.
    /// </summary>
    /// <param name="identifier">The server claim identifier granted to the user.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.View)]
    [InlineData(Claims.ResourceBased.Server.All)]
    public async Task DirectServerClaim_ListsUserForThatServerOnly(string identifier)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var server = Guid.NewGuid();
        var other = Guid.NewGuid();
        var user = CreateUser("direct");
        user.Claims!.Add(await claims.GetAsync(identifier, server.ToString()));
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetServerViewAudienceAsync([server, other]);

        Assert.Equal([user.PrimaryKey], audience[server]);
        Assert.Empty(audience[other]);
    }

    /// <summary>
    /// A user whose role holds the view or all-actions claim bound to one server sees that server and no other.
    /// </summary>
    /// <param name="identifier">The server claim identifier granted to the role.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.View)]
    [InlineData(Claims.ResourceBased.Server.All)]
    public async Task RoleServerClaim_ListsMemberForThatServerOnly(string identifier)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var server = Guid.NewGuid();
        var other = Guid.NewGuid();
        var user = CreateUser("role-member");
        user.Roles!.Add(CreateRole("viewers", await claims.GetAsync(identifier, server.ToString())));
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetServerViewAudienceAsync([server, other]);

        Assert.Equal([user.PrimaryKey], audience[server]);
        Assert.Empty(audience[other]);
    }

    /// <summary>
    /// An empty or null valued global, server or hosting administrative claim, held directly or through a role,
    /// lists the user for every requested server.
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
    public async Task EmptyAdministrativeClaim_ListsUserForEveryServer(
        string identifier,
        string? value,
        bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var servers = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var user = CreateUser("administrator");
        var claim = await claims.GetAsync(identifier, value);
        if (throughRole)
            user.Roles!.Add(CreateRole("administrators", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetServerViewAudienceAsync(servers);

        Assert.All(servers, server => Assert.Equal([user.PrimaryKey], audience[server]));
    }

    /// <summary>
    /// Claims the REST fleet rule does not accept for viewing never list a user, whether held directly or through
    /// a role.
    /// </summary>
    /// <param name="identifier">The claim identifier granted.</param>
    /// <param name="valueKind">
    /// Which value is stored: <c>null</c>, <c>server</c> (the server id), <c>upper</c> (the server id in upper-case
    /// hex), <c>other</c> (another server's id) or <c>text</c> (a non-empty non-id text).
    /// </param>
    /// <param name="throughRole">Whether the claim is granted through a role instead of directly.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(Claims.Creation.Server, "null", false)]
    [InlineData(Claims.Creation.Server, "server", false)]
    [InlineData(Claims.ResourceBased.Server.View, "null", false)]
    [InlineData(Claims.ResourceBased.Server.All, "null", false)]
    [InlineData(Claims.ResourceBased.Server.Operate, "server", false)]
    [InlineData(Claims.Administrative.Event, "null", false)]
    [InlineData(Claims.Administrative.All, "text", false)]
    [InlineData(Claims.ResourceBased.Server.View, "upper", false)]
    [InlineData(Claims.ResourceBased.Server.View, "other", false)]
    [InlineData(Claims.ResourceBased.Server.Operate, "server", true)]
    [InlineData(Claims.Administrative.Event, "null", true)]
    [InlineData(Claims.ResourceBased.Server.View, "other", true)]
    public async Task NonViewingClaim_NeverListsUser(string identifier, string valueKind, bool throughRole)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var server = Guid.NewGuid();
        var value = valueKind switch
        {
            "null"   => null,
            "server" => server.ToString(),
            "upper"  => server.ToString().ToUpperInvariant(),
            "other"  => Guid.NewGuid().ToString(),
            "text"   => "not-empty",
            _        => throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, null),
        };
        var user = CreateUser("not-a-viewer");
        var claim = await claims.GetAsync(identifier, value);
        if (throughRole)
            user.Roles!.Add(CreateRole("not-viewers", claim));
        else
            user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetServerViewAudienceAsync([server]);

        Assert.Empty(audience[server]);
    }

    /// <summary>
    /// Banned and deleted users are listed for no server, even with an otherwise sufficient claim.
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
        var server = Guid.NewGuid();
        var user = CreateUser("excluded");
        user.IsBanned = banned;
        user.IsDeleted = deleted;
        user.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.Server.View, server.ToString()));
        user.Claims!.Add(await claims.GetAsync(Claims.Administrative.Hosting, string.Empty));
        user.Roles!.Add(CreateRole("viewers", await claims.GetAsync(Claims.ResourceBased.Server.All, server.ToString())));
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetServerViewAudienceAsync([server]);

        Assert.Empty(audience[server]);
    }

    /// <summary>
    /// One call resolves several servers: every requested id has an entry, an unviewable server maps to an empty
    /// collection and a user matching through both a user claim and a role is listed once.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task ManyServers_ResolveInOneCallWithoutDuplicates()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var unviewable = Guid.NewGuid();
        var both = CreateUser("both");
        var viewClaim = await claims.GetAsync(Claims.ResourceBased.Server.View, first.ToString());
        both.Claims!.Add(viewClaim);
        both.Roles!.Add(CreateRole("first-viewers", viewClaim));
        var secondOnly = CreateUser("second-only");
        secondOnly.Claims!.Add(await claims.GetAsync(Claims.ResourceBased.Server.All, second.ToString()));
        store.DbContext.Users.AddRange(both, secondOnly);
        await store.DbContext.SaveChangesAsync();

        var audience = await CreateService(store).GetServerViewAudienceAsync([first, second, unviewable, first]);

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

        var audience = await CreateService(store).GetServerViewAudienceAsync([]);

        Assert.Empty(audience);
    }

    /// <summary>
    /// Removing the claim and saving drops the user from the very next result, proving nothing is cached.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task RemovedClaim_DropsUserFromNextResult()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var server = Guid.NewGuid();
        var user = CreateUser("revoked");
        var claim = await claims.GetAsync(Claims.ResourceBased.Server.View, server.ToString());
        user.Claims!.Add(claim);
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        Assert.Equal([user.PrimaryKey], (await service.GetServerViewAudienceAsync([server]))[server]);

        user.Claims.Remove(claim);
        await store.DbContext.SaveChangesAsync();

        Assert.Empty((await service.GetServerViewAudienceAsync([server]))[server]);
    }

    /// <summary>
    /// For every row of a claim matrix, audience membership equals <see cref="IHostingAuthorizationService.HasServerPermission"/>
    /// with the view permission evaluated on the principal <see cref="IHostingAuthorizationService.RebuildPrincipalAsync"/>
    /// builds, where a missing principal means not visible.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Audience_MatchesHasServerPermissionForEveryMatrixRow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var claims = new ClaimRows(store.DbContext);
        var server = Guid.NewGuid();
        var other = Guid.NewGuid();
        var s = server.ToString();
        var t = other.ToString();
        var rows = new MatrixRow[]
        {
            new("no claims", [], [], false, false),
            new("direct sv:view S", [(Claims.ResourceBased.Server.View, s)], [], false, false),
            new("direct sv S", [(Claims.ResourceBased.Server.All, s)], [], false, false),
            new("role sv:view S", [], [(Claims.ResourceBased.Server.View, s)], false, false),
            new("role sv S", [], [(Claims.ResourceBased.Server.All, s)], false, false),
            new("direct sv:view T", [(Claims.ResourceBased.Server.View, t)], [], false, false),
            new("user and role sv:view S", [(Claims.ResourceBased.Server.View, s)],
                [(Claims.ResourceBased.Server.View, s)], false, false),
            new("adm empty", [(Claims.Administrative.All, string.Empty)], [], false, false),
            new("adm null", [(Claims.Administrative.All, null)], [], false, false),
            new("adm:sv empty", [(Claims.Administrative.Server, string.Empty)], [], false, false),
            new("adm:sv null", [(Claims.Administrative.Server, null)], [], false, false),
            new("adm:hosting empty", [(Claims.Administrative.Hosting, string.Empty)], [], false, false),
            new("adm:hosting null", [(Claims.Administrative.Hosting, null)], [], false, false),
            new("role adm:hosting null", [], [(Claims.Administrative.Hosting, null)], false, false),
            new("role adm empty", [], [(Claims.Administrative.All, string.Empty)], false, false),
            new("mixed-case ADM:Hosting empty", [("ADM:Hosting", string.Empty)], [], false, false),
            new("mixed-case SV:View S", [("SV:View", s)], [], false, false),
            new("role mixed-case Sv S", [], [("Sv", s)], false, false),
            new("crt:sv null", [(Claims.Creation.Server, null)], [], false, false),
            new("crt:sv S", [(Claims.Creation.Server, s)], [], false, false),
            new("bare sv:view", [(Claims.ResourceBased.Server.View, null)], [], false, false),
            new("bare sv", [(Claims.ResourceBased.Server.All, null)], [], false, false),
            new("sv:operate S", [(Claims.ResourceBased.Server.Operate, s)], [], false, false),
            new("role sv:operate S", [], [(Claims.ResourceBased.Server.Operate, s)], false, false),
            new("adm:ev empty", [(Claims.Administrative.Event, string.Empty)], [], false, false),
            new("adm non-empty", [(Claims.Administrative.All, "x")], [], false, false),
            new("sv:view upper-case S", [(Claims.ResourceBased.Server.View, s.ToUpperInvariant())], [], false, false),
            new("banned sv:view S", [(Claims.ResourceBased.Server.View, s)], [], true, false),
            new("deleted adm empty", [(Claims.Administrative.All, string.Empty)], [], false, true),
            new("banned role adm:sv empty", [], [(Claims.Administrative.Server, string.Empty)], true, false),
        };
        var users = new List<(MatrixRow Row, User User)>();
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
            users.Add((row, user));
        }

        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var audience = await service.GetServerViewAudienceAsync([server, other]);

        foreach (var (row, user) in users)
        {
            var principal = await service.RebuildPrincipalAsync(user.PrimaryKey);
            foreach (var serverId in new[] { server, other })
            {
                var expected = principal is not null
                               && service.HasServerPermission(
                                   principal,
                                   serverId,
                                   Claims.ResourceBased.Server.View);
                var actual = audience[serverId].Contains(user.PrimaryKey);
                Assert.True(
                    expected == actual,
                    $"Row '{row.Name}' on server {(serverId == server ? "S" : "T")}: "
                    + $"HasServerPermission={expected}, audience={actual}.");
            }
        }

        Assert.All(
            audience.Values,
            entry => Assert.Equal(entry.Count, entry.Distinct().Count()));
    }

    /// <summary>
    /// The three audience query builders translate for Npgsql and filter on the banned and deleted columns, proven
    /// offline with <c>ToQueryString</c> on a metadata-only Npgsql context that never connects.
    /// </summary>
    [Fact]
    public void AudienceQueries_TranslateForNpgsql()
    {
        using var dbContext = CreateNpgsqlModelDbContext();
        var users = dbContext.Users.AsNoTracking();
        string[] serverValues = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()];

        var queries = new[]
        {
            HostingAuthorizationService.BuildGlobalViewerQuery(users).ToQueryString(),
            HostingAuthorizationService.BuildDirectViewGrantQuery(users, serverValues).ToQueryString(),
            HostingAuthorizationService.BuildRoleViewGrantQuery(users, serverValues).ToQueryString(),
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
    /// One row of the drift matrix: the claims a user holds directly and through one role, and the user's flags.
    /// </summary>
    /// <param name="Name">The row name used in assertion messages and as the user's nickname.</param>
    /// <param name="UserClaims">The claims granted directly to the user as identifier and stored value.</param>
    /// <param name="RoleClaims">The claims granted through one role as identifier and stored value.</param>
    /// <param name="Banned">Whether the user is banned.</param>
    /// <param name="Deleted">Whether the user is deleted.</param>
    private sealed record MatrixRow(
        string Name,
        IReadOnlyList<(string Identifier, string? Value)> UserClaims,
        IReadOnlyList<(string Identifier, string? Value)> RoleClaims,
        bool Banned,
        bool Deleted);

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
