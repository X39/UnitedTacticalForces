using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using Xunit;
using DataClaim = X39.UnitedTacticalForces.Database.Authority.Claim;

namespace Api.Tests.Authority;

public class PersistenceInvariantTests
{
    [Fact]
    public void ApiDbContext_ConfiguresClaimUniqueIndexesForNullableValues()
    {
        using var dbContext = CreateNpgsqlModelDbContext();
        var claimEntityType = dbContext.Model.FindEntityType(typeof(DataClaim));

        Assert.NotNull(claimEntityType);
        AssertClaimIndex(
            claimEntityType,
            DataClaim.UniqueClaimValueIndexName,
            ["Identifier", "Value", "ValueType"],
            DataClaim.UniqueClaimValueIndexFilter
        );
        AssertClaimIndex(
            claimEntityType,
            DataClaim.UniqueClaimNullValueTypeIndexName,
            ["Identifier", "Value"],
            DataClaim.UniqueClaimNullValueTypeIndexFilter
        );
        AssertClaimIndex(
            claimEntityType,
            DataClaim.UniqueClaimNullValueIndexName,
            ["Identifier", "ValueType"],
            DataClaim.UniqueClaimNullValueIndexFilter
        );
        AssertClaimIndex(
            claimEntityType,
            DataClaim.UniqueClaimNullValueAndValueTypeIndexName,
            ["Identifier"],
            DataClaim.UniqueClaimNullValueAndValueTypeIndexFilter
        );
    }

    [Fact]
    public void ApiDbContext_ConfiguresActiveSteamIdUniqueIndex()
    {
        using var dbContext = CreateNpgsqlModelDbContext();
        var steamEntityType = Assert.Single(
            dbContext.Model.GetEntityTypes(),
            q => q.ClrType == typeof(SteamUser)
        );

        var index = Assert.Single(
            steamEntityType.GetIndexes(),
            q => q.GetDatabaseName() == User.ActiveSteamId64IndexName
        );

        Assert.True(index.IsUnique);
        Assert.Equal(["Id64"], index.Properties.Select(q => q.Name).ToArray());
        Assert.Equal(User.ActiveSteamId64IndexFilter, index.GetFilter());
    }

    [Fact]
    public async Task AddResourceClaim_ReusesExistingNullValueTypeClaimRowForUsersAndRoles()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("modpack-operator");
        var role = new Role
        {
            PrimaryKey  = 10,
            Title       = "Mod Pack Staff",
            Description = "Can operate selected mod packs",
        };
        var existingClaim = new DataClaim
        {
            Identifier = Claims.ResourceBased.ModPack.Modify,
            Value      = "modpack-1",
            ValueType  = null,
        };
        dbContext.Users.Add(user);
        dbContext.Roles.Add(role);
        dbContext.Claims.Add(existingClaim);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var controller = CreateResourceClaimController(dbContext);

        var userResult = await controller.AddUserResourceClaim(
            Claims.ResourceBased.ModPack.All,
            existingClaim.Value,
            user.PrimaryKey,
            existingClaim.Identifier
        );
        var roleResult = await controller.AddRoleResourceClaim(
            Claims.ResourceBased.ModPack.All,
            existingClaim.Value,
            (int) role.PrimaryKey,
            existingClaim.Identifier
        );

        Assert.IsType<NoContentResult>(userResult);
        Assert.IsType<NoContentResult>(roleResult);
        Assert.Equal(
            1,
            await dbContext.Claims.CountAsync(q
                => q.Identifier == existingClaim.Identifier
                   && q.Value == existingClaim.Value
                   && q.ValueType == null)
        );
        dbContext.ChangeTracker.Clear();

        var userClaimId = await dbContext.Users
            .Where(q => q.PrimaryKey == user.PrimaryKey)
            .SelectMany(q => q.Claims!)
            .Select(q => q.PrimaryKey)
            .SingleAsync();
        var roleClaimId = await dbContext.Roles
            .Where(q => q.PrimaryKey == role.PrimaryKey)
            .SelectMany(q => q.Claims!)
            .Select(q => q.PrimaryKey)
            .SingleAsync();

        Assert.Equal(existingClaim.PrimaryKey, userClaimId);
        Assert.Equal(existingClaim.PrimaryKey, roleClaimId);
    }

    private static ResourceClaimController CreateResourceClaimController(DatabaseContext dbContext)
        => new(NullLogger<ResourceClaimController>.Instance, dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private static void AssertClaimIndex(
        IEntityType claimEntityType,
        string indexName,
        string[] propertyNames,
        string filter)
    {
        var index = Assert.Single(
            claimEntityType.GetIndexes(),
            q => q.GetDatabaseName() == indexName
        );
        Assert.True(index.IsUnique);
        Assert.Equal(propertyNames, index.Properties.Select(q => q.Name).ToArray());
        Assert.Equal(filter, index.GetFilter());
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

    private static DatabaseContext CreateNpgsqlModelDbContext()
    {
        var options = new DbContextOptionsBuilder<NpgsqlApiDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=metadata-only;Username=metadata;Password=metadata",
                npgsqlOptions => npgsqlOptions.UseNodaTime())
            .Options;
        return new NpgsqlApiDbContext(options);
    }

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

    private sealed class NpgsqlApiDbContext(DbContextOptions<NpgsqlApiDbContext> dbContextOptions)
        : DatabaseContext(dbContextOptions);
}
