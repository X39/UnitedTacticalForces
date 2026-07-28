using Api.Tests.GameServerHosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;
using Xunit;

namespace Api.Tests.ModPacks;

public sealed class GameScopedCreationTests
{
    [Fact]
    public async Task StandaloneCreationCanonicalizesRegisteredGameIdentifier()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await HostingTestData.SeedServerAsync(dbContext);
        var controller = CreateController(dbContext, seed.User);

        var result = await controller.CreateModPackStandaloneAsync(
            new ModPackCreationPayload
            {
                GameIdentifier = HostingTestData.GameIdentifier.ToUpperInvariant(),
                Title          = "Scoped pack",
                Html           = "<html></html>",
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var created = await dbContext.ModPackDefinitions.SingleAsync();
        Assert.Equal(HostingTestData.GameIdentifier, created.GameIdentifier);
    }

    [Fact]
    public async Task CompositionCreationRejectsRevisionFromAnotherGame()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await HostingTestData.SeedServerAsync(dbContext);
        var existingDefinition = new ModPackDefinition
        {
            GameIdentifier        = "another-game",
            TimeStampCreated      = Instant.FromUnixTimeSeconds(1_700_000_000),
            Title                 = "Other game pack",
            OwnerFk               = seed.User.PrimaryKey,
            IsActive              = true,
            ModPackRevisions      = [],
            ModPackRevisionsOwned = [],
        };
        var revision = new ModPackRevision
        {
            TimeStampCreated = existingDefinition.TimeStampCreated,
            Html             = "<html></html>",
            UpdatedByFk      = seed.User.PrimaryKey,
            IsActive         = true,
            Definition       = existingDefinition,
        };
        existingDefinition.ModPackRevisions.Add(revision);
        existingDefinition.ModPackRevisionsOwned.Add(revision);
        dbContext.ModPackDefinitions.Add(existingDefinition);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, seed.User);

        var result = await controller.CreateModPackCompositionAsync(
            new ModPackCompositionCreationPayload
            {
                GameIdentifier = HostingTestData.GameIdentifier,
                Title          = "Invalid composition",
                RevisionIds    = [revision.PrimaryKey],
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, await dbContext.ModPackDefinitions.CountAsync());
    }

    private static ModPackController CreateController(
        DatabaseContext dbContext,
        X39.UnitedTacticalForces.Database.Authority.User user)
        => new(
            NullLogger<ModPackController>.Instance,
            dbContext,
            new TestGameServerDefinitionCatalog())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = HostingTestData.Principal(user),
                },
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
