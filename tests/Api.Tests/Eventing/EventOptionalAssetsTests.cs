using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SecurityClaim = System.Security.Claims.Claim;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Core;
using Xunit;

namespace Api.Tests.Eventing;

public class EventOptionalAssetsTests
{
    [Fact]
    public async Task CreateEvent_PublicVisibilityPersistsAndIsExposedToAnonymousUpcomingQuery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        dbContext.Users.Add(host);
        await dbContext.SaveChangesAsync();

        var createResult = await CreateController(dbContext, host)
            .CreateEventAsync(CreatePayload(host.PrimaryKey) with { IsVisible = true }, default);
        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(createResult).Value);

        Assert.True(await dbContext.Events
            .Where(q => q.PrimaryKey == created.PrimaryKey)
            .Select(q => q.IsVisible)
            .SingleAsync());
        dbContext.ChangeTracker.Clear();

        var upcomingResult = await CreateAnonymousController(dbContext).GetUpcomingEventsAsync(default);
        var upcoming = Assert.IsType<UpcomingEventDto[]>(Assert.IsType<OkObjectResult>(upcomingResult).Value);
        Assert.Contains(upcoming, q => q.PrimaryKey == created.PrimaryKey);
    }

    [Fact]
    public async Task CreateEvent_AllowsNoTerrainModPackImageOrSlots_AndProjectsNulls()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        dbContext.Users.Add(host);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, host);

        var result = await controller.CreateEventAsync(CreatePayload(host.PrimaryKey), default);

        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Null(created.TerrainFk);
        Assert.Null(created.ModPackRevisionFk);
        Assert.Null(created.ImageAssetFk);
        Assert.Null(created.ImagePath);
        var stored = await dbContext.Events.SingleAsync();
        Assert.Null(stored.TerrainFk);
        Assert.Null(stored.ModPackRevisionFk);
        Assert.Null(stored.ImageAssetFk);

        dbContext.ChangeTracker.Clear();
        var projected = Assert.IsType<UpcomingEventDto[]>(
            Assert.IsType<OkObjectResult>(await controller.GetUpcomingEventsAsync(default)).Value);
        var eventItem = Assert.Single(projected);
        Assert.Equal(string.Empty, eventItem.TerrainTitle);
        Assert.Equal(string.Empty, eventItem.ModPackDefinitionTitle);
        Assert.Null(eventItem.ImageAssetFk);
        Assert.Null(eventItem.ImagePath);
    }

    [Fact]
    public async Task UpdateEvent_CanAddAndExplicitlyClearOptionalAssets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        var imageAssetFk = Guid.NewGuid();
        var terrain = new Terrain { PrimaryKey = 10, GameIdentifier = "arma3", Title = "Altis", IsActive = true };
        var terrainVersion = new TerrainVersion
        {
            PrimaryKey = Guid.NewGuid(),
            Terrain = terrain,
            VersionNumber = 1,
            VersionLabel = "1",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        var definition = new ModPackDefinition
        {
            PrimaryKey = 20,
            GameIdentifier = "arma3",
            Title = "Operations",
            Owner = host,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        var revision = new ModPackRevision
        {
            PrimaryKey = 30,
            Definition = definition,
            UpdatedBy = host,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        dbContext.AddRange(host, terrain, terrainVersion, definition, revision,
            Api.Tests.MediaAssetTestData.Create(imageAssetFk));
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, host);
        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(
            await controller.CreateEventAsync(CreatePayload(host.PrimaryKey), default)).Value);

        Assert.IsType<NoContentResult>(await controller.UpdateEventAsync(created.PrimaryKey, new EventUpdate
        {
            TerrainFk = terrain.PrimaryKey,
            TerrainVersionFk = terrainVersion.PrimaryKey,
            ModPackRevisionFk = revision.PrimaryKey,
            ImageAssetFk = imageAssetFk,
        }, default));
        var stored = await dbContext.Events.SingleAsync(q => q.PrimaryKey == created.PrimaryKey);
        Assert.Equal(terrain.PrimaryKey, stored.TerrainFk);
        Assert.Equal(terrainVersion.PrimaryKey, stored.PinnedTerrainVersionFk);
        Assert.Equal(revision.PrimaryKey, stored.ModPackRevisionFk);
        Assert.Equal(imageAssetFk, stored.ImageAssetFk);

        Assert.IsType<NoContentResult>(await controller.UpdateEventAsync(created.PrimaryKey, new EventUpdate
        {
            ClearTerrain = true,
            ClearModPackRevision = true,
            ClearImageAsset = true,
        }, default));
        Assert.Null(stored.TerrainFk);
        Assert.Null(stored.PinnedTerrainVersionFk);
        Assert.Null(stored.ModPackRevisionFk);
        Assert.Null(stored.ImageAssetFk);
    }

    [Fact]
    public async Task CreateAndUpdate_RejectInvalidScopeAndConflictingClearRequests()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        var terrain = new Terrain { PrimaryKey = 10, GameIdentifier = "arma3", Title = "Altis", IsActive = true };
        var definition = new ModPackDefinition
        {
            PrimaryKey = 20,
            GameIdentifier = "cs2",
            Title = "CS2",
            Owner = host,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        var revision = new ModPackRevision
        {
            PrimaryKey = 30,
            Definition = definition,
            UpdatedBy = host,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        dbContext.AddRange(host, terrain, definition, revision);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, host);

        Assert.IsType<BadRequestObjectResult>(await controller.CreateEventAsync(
            CreatePayload(host.PrimaryKey) with
            {
                TerrainFk = terrain.PrimaryKey,
                ModPackRevisionFk = revision.PrimaryKey,
            }, default));

        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(
            await controller.CreateEventAsync(CreatePayload(host.PrimaryKey), default)).Value);
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateEventAsync(created.PrimaryKey, new EventUpdate
        {
            TerrainFk = terrain.PrimaryKey,
            ClearTerrain = true,
        }, default));
    }

    private static EventCreationPayload CreatePayload(Guid hostId)
    {
        var scheduledFor = NodaSystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(1));
        return new EventCreationPayload
        {
            Title = "Optional assets operation",
            Description = "Briefing",
            ScheduledFor = scheduledFor,
            HostedByFk = hostId,
            IsVisible = true,
            MinimumAccepted = 0,
        };
    }

    private static EventsController CreateController(DatabaseContext dbContext, User user)
        => new(
            NullLogger<EventsController>.Instance,
            dbContext,
            null,
            new NoopUpdateStreamService(),
            new BaseUrl("https://api.example/", "https://client.example/"),
            new MediaAssetService(dbContext))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(user) },
            },
        };

    private static EventsController CreateAnonymousController(DatabaseContext dbContext)
        => new(
            NullLogger<EventsController>.Instance,
            dbContext,
            null,
            new NoopUpdateStreamService(),
            new BaseUrl("https://api.example/", "https://client.example/"),
            new MediaAssetService(dbContext))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
            },
        };

    private static ClaimsPrincipal CreatePrincipal(User user)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new SecurityClaim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = nickname,
            AvatarMimeType = string.Empty,
            Discord = new DiscordUser { Username = nickname },
            Steam = new SteamUser(),
        };

    private static async Task<DatabaseContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SqliteDatabaseContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new SqliteDatabaseContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private sealed class NoopUpdateStreamService : IUpdateStreamService
    {
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask SendUpdateAsync<T>(string path, T package)
            => ValueTask.CompletedTask;
    }

    private sealed class SqliteDatabaseContext(DbContextOptions<SqliteDatabaseContext> options)
        : DatabaseContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<Instant>().HaveConversion<InstantToLongConverter>();
            configurationBuilder.Properties<LocalDate>().HaveConversion<LocalDateToIntConverter>();
        }
    }
}
