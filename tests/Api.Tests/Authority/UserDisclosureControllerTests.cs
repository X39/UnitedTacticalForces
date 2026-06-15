using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using SecurityClaim = System.Security.Claims.Claim;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Api.DTO;
using Xunit;

namespace Api.Tests.Authority;

public class UserDisclosureControllerTests
{
    [Fact]
    public async Task CurrentUserProjections_ReturnNullForUnlinkedExternalAccounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("actor", 111, 0);
        actor.Discord.Username = string.Empty;
        dbContext.Users.Add(actor);
        await dbContext.SaveChangesAsync();
        var controller = CreateUsersController(dbContext, CreatePrincipal(actor));

        var currentUser = await InvokeOkAsync<FullUserDto>(
            controller.GetMeAsync(CancellationToken.None)
        );
        var profileSettings = await InvokeOkAsync<ProfileSettingsDto>(
            controller.GetMeSettingsAsync(CancellationToken.None)
        );

        Assert.Equal(actor.Steam.Id64.ToString(), currentUser.SteamId64);
        Assert.Null(currentUser.DiscordId);
        Assert.Equal(string.Empty, currentUser.DiscordUsername);
        Assert.Equal(actor.Steam.Id64.ToString(), profileSettings.SteamId64);
        Assert.Null(profileSettings.DiscordId);
        Assert.Equal(string.Empty, profileSettings.DiscordUsername);
    }

    [Fact]
    public async Task GetUsersAsync_RedactsSensitiveFieldsWithSameRulesAsGetUserAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("actor", 111, 222);
        var target = CreateUser("target", 333, 444);
        dbContext.Users.AddRange(actor, target);
        await dbContext.SaveChangesAsync();
        var controller = CreateUsersController(dbContext, CreatePrincipal(actor));

        var listedUsers = await InvokeOkAsync<IEnumerable<FullUserDto>>(
            controller.GetUsersAsync(0, 20, CancellationToken.None)
        );
        var listedTarget = listedUsers.Single(q => q.PrimaryKey == target.PrimaryKey);
        var singleTarget = await InvokeOkAsync<UserDto>(
            controller.GetUserAsync(target.PrimaryKey, CancellationToken.None)
        );

        Assert.Equal(singleTarget.EMail, listedTarget.EMail);
        Assert.Equal(singleTarget.SteamId64, listedTarget.SteamId64);
        Assert.Equal(singleTarget.DiscordId, listedTarget.DiscordId);
        Assert.Equal(singleTarget.DiscordUsername, listedTarget.DiscordUsername);
        Assert.Equal(singleTarget.IsBanned, listedTarget.IsBanned);
        Assert.Equal(singleTarget.IsVerified, listedTarget.IsVerified);
        Assert.NotEqual(target.EMail, listedTarget.EMail);
        Assert.NotEqual(target.Steam.Id64.ToString(), listedTarget.SteamId64);
        Assert.NotEqual(target.Discord.Id.ToString(), listedTarget.DiscordId);
        Assert.NotEqual(target.Discord.Username, listedTarget.DiscordUsername);
        Assert.False(listedTarget.IsBanned);
        Assert.False(listedTarget.IsVerified);

        var listedActor = listedUsers.Single(q => q.PrimaryKey == actor.PrimaryKey);
        Assert.Equal(actor.EMail, listedActor.EMail);
        Assert.Equal(actor.Steam.Id64.ToString(), listedActor.SteamId64);
        Assert.Equal(actor.Discord.Id.ToString(), listedActor.DiscordId);
        Assert.Equal(actor.Discord.Username, listedActor.DiscordUsername);
        Assert.True(listedActor.IsBanned);
        Assert.True(listedActor.IsVerified);
    }

    [Fact]
    public async Task GetUsersAsync_AllowsPerFieldSensitiveAccessForOtherUsers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("actor", 111, 222);
        var target = CreateUser("target", 333, 444);
        dbContext.Users.AddRange(actor, target);
        await dbContext.SaveChangesAsync();
        var controller = CreateUsersController(
            dbContext,
            CreatePrincipal(
                actor,
                (Claims.User.EMail, string.Empty),
                (Claims.User.ViewSteamId64, string.Empty),
                (Claims.User.ViewDiscordId, string.Empty),
                (Claims.User.Ban, string.Empty),
                (Claims.User.Verify, string.Empty)
            )
        );

        var listedUsers = await InvokeOkAsync<IEnumerable<FullUserDto>>(
            controller.GetUsersAsync(0, 20, CancellationToken.None)
        );
        var listedTarget = listedUsers.Single(q => q.PrimaryKey == target.PrimaryKey);

        AssertSensitiveFieldsVisible(target, listedTarget);
    }

    [Fact]
    public async Task GetUsersAsync_AllowsUserAdminSensitiveAccessForOtherUsers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("actor", 111, 222);
        var target = CreateUser("target", 333, 444);
        dbContext.Users.AddRange(actor, target);
        await dbContext.SaveChangesAsync();
        var controller = CreateUsersController(
            dbContext,
            CreatePrincipal(actor, (Claims.Administrative.User, string.Empty))
        );

        var listedUsers = await InvokeOkAsync<IEnumerable<FullUserDto>>(
            controller.GetUsersAsync(0, 20, CancellationToken.None)
        );
        var listedTarget = listedUsers.Single(q => q.PrimaryKey == target.PrimaryKey);

        AssertSensitiveFieldsVisible(target, listedTarget);
    }

    private static void AssertSensitiveFieldsVisible(User expectedUser, FullUserDto actualUser)
    {
        Assert.Equal(expectedUser.EMail, actualUser.EMail);
        Assert.Equal(expectedUser.Steam.Id64.ToString(), actualUser.SteamId64);
        Assert.Equal(expectedUser.Discord.Id.ToString(), actualUser.DiscordId);
        Assert.Equal(expectedUser.Discord.Username, actualUser.DiscordUsername);
        Assert.Equal(expectedUser.IsBanned, actualUser.IsBanned);
        Assert.Equal(expectedUser.IsVerified, actualUser.IsVerified);
    }

    private static async Task<T> InvokeOkAsync<T>(Task<IActionResult> actionTask)
    {
        var actionResult = await actionTask;
        return Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(actionResult).Value);
    }

    private static UsersController CreateUsersController(DatabaseContext dbContext, ClaimsPrincipal user)
        => new(NullLogger<UsersController>.Instance, dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                },
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

    private static User CreateUser(string nickname, ulong steamId64, ulong discordId)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            EMail          = $"{nickname}@example.test",
            IsBanned       = true,
            IsVerified     = true,
            Avatar         = [1, 2, 3],
            AvatarMimeType = "image/png",
            Discord = new DiscordUser
            {
                Id       = discordId,
                Username = $"{nickname}-discord",
            },
            Steam = new SteamUser
            {
                Id64 = steamId64,
            },
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
