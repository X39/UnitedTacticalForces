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
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Common.Event;
using Xunit;
using UtfClaims = X39.UnitedTacticalForces.Claims;
using ApiEvent = X39.UnitedTacticalForces.Database.Eventing.Event;

namespace Api.Tests.Eventing;

public class EventVisibilityMutationTests
{
    [Fact]
    public async Task AcceptHiddenEvent_RequiresVisibilityHostOrAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        var participant = CreateUser("participant");
        var hiddenEvent = CreateEvent(host, isVisible: false);
        SeedMediaAssets(dbContext, hiddenEvent);
        dbContext.Users.AddRange(host, participant);
        dbContext.Events.Add(hiddenEvent);
        await dbContext.SaveChangesAsync();

        var participantController = CreateEventsController(dbContext, CreatePrincipal(participant));
        var hostController = CreateEventsController(dbContext, CreatePrincipal(host));

        Assert.IsType<ForbidResult>(await participantController.AcceptEventAsync(hiddenEvent.PrimaryKey, default));
        Assert.Empty(dbContext.UserEventMetas);

        Assert.IsType<NoContentResult>(await hostController.AcceptEventAsync(hiddenEvent.PrimaryKey, default));
        var meta = await dbContext.UserEventMetas.SingleAsync();
        Assert.Equal(host.PrimaryKey, meta.UserFk);
        Assert.Equal(EEventAcceptance.Accepted, meta.Acceptance);
    }

    [Fact]
    public async Task AssignSelfToHiddenEventSlot_RequiresVisibilityHostOrAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        var participant = CreateUser("participant");
        var hiddenEvent = CreateEvent(host, isVisible: false);
        hiddenEvent.UserMetas = [];
        SeedMediaAssets(dbContext, hiddenEvent);
        dbContext.Users.AddRange(host, participant);
        dbContext.Events.Add(hiddenEvent);
        dbContext.EventSlots.Add(new EventSlot
        {
            Event            = hiddenEvent,
            SlotNumber       = 1,
            IsVisible        = true,
            IsSelfAssignable = true,
            Group            = "Alpha",
            Title            = "Rifleman",
        });
        await dbContext.SaveChangesAsync();

        var participantController = CreateSlottingController(dbContext, CreatePrincipal(participant));
        var hostController = CreateSlottingController(dbContext, CreatePrincipal(host));

        Assert.IsType<ForbidResult>(
            await participantController.AssignEventSlotToSelf(hiddenEvent.PrimaryKey, 1, default)
        );
        Assert.Null(await dbContext.EventSlots.Select(q => q.AssignedToFk).SingleAsync());

        Assert.IsType<NoContentResult>(
            await hostController.AssignEventSlotToSelf(hiddenEvent.PrimaryKey, 1, default)
        );
        Assert.Equal(host.PrimaryKey, await dbContext.EventSlots.Select(q => q.AssignedToFk).SingleAsync());
    }

    [Fact]
    public async Task AssignSelfToHiddenEventSlot_AllowsEventAdministrator()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser("event-host");
        var administrator = CreateUser("event-admin");
        var hiddenEvent = CreateEvent(host, isVisible: false);
        SeedMediaAssets(dbContext, hiddenEvent);
        dbContext.Users.AddRange(host, administrator);
        dbContext.Events.Add(hiddenEvent);
        dbContext.EventSlots.Add(new EventSlot
        {
            Event            = hiddenEvent,
            SlotNumber       = 1,
            IsVisible        = false,
            IsSelfAssignable = false,
            Group            = "Alpha",
            Title            = "Zeus",
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateSlottingController(
            dbContext,
            CreatePrincipal(administrator, (UtfClaims.Administrative.Event, string.Empty))
        );

        Assert.IsType<NoContentResult>(await controller.AssignEventSlotToSelf(hiddenEvent.PrimaryKey, 1, default));
        Assert.Equal(administrator.PrimaryKey, await dbContext.EventSlots.Select(q => q.AssignedToFk).SingleAsync());
    }

    private static EventsController CreateEventsController(DatabaseContext dbContext, ClaimsPrincipal user)
        => new(
            NullLogger<EventsController>.Instance,
            dbContext,
            null,
            new NoopUpdateStreamService(),
            new BaseUrl("https://api.example/", "https://client.example/"),
            new MediaAssetService(dbContext)
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                },
            },
        };

    private static EventSlottingController CreateSlottingController(DatabaseContext dbContext, ClaimsPrincipal user)
        => new(
            NullLogger<EventSlottingController>.Instance,
            dbContext,
            new NoopUpdateStreamService()
        )
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

    private static void SeedMediaAssets(DatabaseContext dbContext, ApiEvent appEvent)
    {
        dbContext.MediaAssets.AddRange(
            Api.Tests.MediaAssetTestData.Create(appEvent.Terrain!.ImageAssetFk),
            Api.Tests.MediaAssetTestData.Create(appEvent.ImageAssetFk)
        );
    }

    private static ApiEvent CreateEvent(User host, bool isVisible)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var terrainImageAssetId = Guid.NewGuid();
        var eventImageAssetId = Guid.NewGuid();
        var terrain = new Terrain
        {
            Title        = "Altis",
            ImageAssetFk = terrainImageAssetId,
            IsActive     = true,
        };
        var definition = new ModPackDefinition
        {
            Title            = "Base",
            OwnerFk          = host.PrimaryKey,
            TimeStampCreated = now,
            IsActive         = true,
        };
        var revision = new ModPackRevision
        {
            Definition       = definition,
            UpdatedByFk      = host.PrimaryKey,
            TimeStampCreated = now,
            IsActive         = true,
        };

        return new ApiEvent
        {
            PrimaryKey            = Guid.NewGuid(),
            Title                 = "Hidden operation",
            Description           = "Classified.",
            Terrain               = terrain,
            ModPackRevision       = revision,
            ImageAssetFk          = eventImageAssetId,
            ScheduledForOriginal  = now.Plus(Duration.FromDays(1)),
            ScheduledFor          = now.Plus(Duration.FromDays(1)),
            TimeStampCreated      = now,
            IsVisible             = isVisible,
            OwnerFk               = host.PrimaryKey,
            HostedByFk            = host.PrimaryKey,
        };
    }

    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Discord = new DiscordUser
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

    private sealed class NoopUpdateStreamService : IUpdateStreamService
    {
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask SendUpdateAsync<T>(string path, T package)
            => ValueTask.CompletedTask;
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
