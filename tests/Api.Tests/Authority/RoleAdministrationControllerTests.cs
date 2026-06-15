using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using SecurityClaim = System.Security.Claims.Claim;
using AuthorityClaim = X39.UnitedTacticalForces.Database.Authority.Claim;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using Xunit;

namespace Api.Tests.Authority;

public class RoleAdministrationControllerTests
{
    [Fact]
    public void CreateRoleAsync_KeepsCreationRolePolicy()
    {
        AssertAuthorizePolicy(
            nameof(RolesController.CreateRoleAsync),
            Claims.Creation.Role,
            typeof(string),
            typeof(string),
            typeof(CancellationToken)
        );
    }

    [Fact]
    public void UpdateRoleAsync_RequiresAdministrativeRolePolicy()
    {
        AssertAuthorizePolicy(
            nameof(RolesController.UpdateRoleAsync),
            Claims.Administrative.Role,
            typeof(long),
            typeof(RoleUpdate),
            typeof(CancellationToken)
        );
    }

    [Fact]
    public void DeleteRoleAsync_RequiresAdministrativeRolePolicy()
    {
        AssertAuthorizePolicy(
            nameof(RolesController.DeleteRoleAsync),
            Claims.Administrative.Role,
            typeof(long),
            typeof(CancellationToken)
        );
    }

    [Fact]
    public void SetRoleClaimToAsync_RequiresAdministrativeRolePolicy()
    {
        AssertAuthorizePolicy(
            nameof(RolesController.SetRoleClaimToAsync),
            Claims.Administrative.Role,
            typeof(long),
            typeof(long),
            typeof(bool),
            typeof(CancellationToken)
        );
    }

    [Fact]
    public async Task CreateRoleAsync_PersistsNormalizedRoleMetadata()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("role-creator");
        dbContext.Users.Add(actor);
        await dbContext.SaveChangesAsync();
        var controller = CreateRolesController(dbContext, CreatePrincipal(actor, (Claims.Creation.Role, string.Empty)));

        var result = await controller.CreateRoleAsync(
            "  Operations Staff  ",
            "  Coordinates operations  ",
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
        var createdRole = await dbContext.Roles.SingleAsync();
        Assert.Equal("Operations Staff", createdRole.Title);
        Assert.Equal("Coordinates operations", createdRole.Description);
    }

    [Fact]
    public async Task CreateRoleAsync_RejectsBlankTitle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("role-creator");
        dbContext.Users.Add(actor);
        await dbContext.SaveChangesAsync();
        var controller = CreateRolesController(dbContext, CreatePrincipal(actor, (Claims.Creation.Role, string.Empty)));

        var result = await controller.CreateRoleAsync("   ", "Description", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Title must not be empty.", badRequest.Value);
        Assert.Empty(await dbContext.Roles.ToArrayAsync());
    }

    [Fact]
    public async Task UpdateRoleAsync_PersistsNormalizedRoleMetadata()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("role-admin");
        var role = new Role
        {
            PrimaryKey  = 10,
            Title       = "Old title",
            Description = "Old description",
        };
        dbContext.Users.Add(actor);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();
        var controller = CreateRolesController(dbContext, CreatePrincipal(actor, (Claims.Administrative.Role, string.Empty)));

        var result = await controller.UpdateRoleAsync(
            role.PrimaryKey,
            new RoleUpdate
            {
                Title       = "  Operations Staff  ",
                Description = "  Coordinates operations  ",
            },
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
        var updatedRole = await dbContext.Roles.SingleAsync(q => q.PrimaryKey == role.PrimaryKey);
        Assert.Equal("Operations Staff", updatedRole.Title);
        Assert.Equal("Coordinates operations", updatedRole.Description);
    }

    [Fact]
    public async Task SetRoleClaimToAsync_AddsAndRemovesRoleClaim()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("role-admin");
        var role = new Role
        {
            PrimaryKey  = 30,
            Title       = "Mission Staff",
            Description = "Can coordinate missions",
            Claims      = [],
        };
        var claim = new AuthorityClaim
        {
            PrimaryKey  = 40,
            Category    = "Operations",
            Title       = "Manage missions",
            Identifier  = Claims.Administrative.Event,
            Description = "Can manage mission events",
        };
        dbContext.Users.Add(actor);
        dbContext.Roles.Add(role);
        dbContext.Claims.Add(claim);
        await dbContext.SaveChangesAsync();
        var controller = CreateRolesController(dbContext, CreatePrincipal(actor, (Claims.Administrative.Role, string.Empty)));

        var addResult = await controller.SetRoleClaimToAsync(
            role.PrimaryKey,
            claim.PrimaryKey,
            mode: true,
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(addResult);
        dbContext.ChangeTracker.Clear();
        var roleWithClaim = await dbContext.Roles
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == role.PrimaryKey);
        Assert.Equal(claim.PrimaryKey, Assert.Single(roleWithClaim.Claims!).PrimaryKey);

        var removeResult = await controller.SetRoleClaimToAsync(
            role.PrimaryKey,
            claim.PrimaryKey,
            mode: false,
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(removeResult);
        dbContext.ChangeTracker.Clear();
        var roleWithoutClaim = await dbContext.Roles
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == role.PrimaryKey);
        Assert.Empty(roleWithoutClaim.Claims ?? []);
    }

    private static RolesController CreateRolesController(DatabaseContext dbContext, ClaimsPrincipal user)
        => new(NullLogger<RolesController>.Instance, dbContext)
        {
            ControllerContext = CreateControllerContext(user),
        };

    private static void AssertAuthorizePolicy(
        string methodName,
        string expectedPolicy,
        params Type[] parameterTypes
    )
    {
        var method = typeof(RolesController).GetMethod(methodName, parameterTypes)
                     ?? throw new InvalidOperationException($"Could not find {methodName}.");
        var authorizeAttribute = Assert.Single(
            method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>()
        );
        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }

    private static ControllerContext CreateControllerContext(ClaimsPrincipal user)
        => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = user,
            },
        };

    private static ClaimsPrincipal CreatePrincipal(User user, params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new SecurityClaim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        foreach (var (type, value) in claims)
            identity.AddClaim(new SecurityClaim(type, value));
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Discord        = new DiscordUser
            {
                Username = nickname,
            },
            Steam = new SteamUser(),
        };

    private static async Task<DatabaseContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SqliteApiDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new SqliteApiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private sealed class SqliteApiDbContext(DbContextOptions<SqliteApiDbContext> dbContextOptions)
        : DatabaseContext(dbContextOptions)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {

            configurationBuilder
                .Properties<Instant>()
                .HaveConversion<InstantToLongConverter>();
            configurationBuilder
                .Properties<LocalDate>()
                .HaveConversion<LocalDateToIntConverter>();
        }
    }
}
