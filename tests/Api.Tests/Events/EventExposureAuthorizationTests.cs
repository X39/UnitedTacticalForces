using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using SecurityClaim = System.Security.Claims.Claim;
using UtfClaims = X39.UnitedTacticalForces.Claims;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Common.Event;
using Xunit;

namespace Api.Tests.Events;

public class EventExposureAuthorizationTests
{
    [Fact]
    public async Task UpcomingEvents_AllowsAnonymousUsersAndHidesInvisibleEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await SeedEventsAsync(dbContext);
        var anonymousController = CreateAnonymousEventsController(dbContext);

        var result = await anonymousController.GetUpcomingEventsAsync(CancellationToken.None);

        var events = Assert.IsType<UpcomingEventDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        var eventItem = Assert.Single(events);
        Assert.Equal(seed.VisibleEventId, eventItem.PrimaryKey);
        Assert.Equal("Visible operation", eventItem.Title);
        Assert.Null(eventItem.MetaAcceptance);
        Assert.Null(eventItem.ModPackRevisionTimeStampDownloaded);
    }

    [Fact]
    public async Task AcceptanceActions_RequireVisibleEventOrEventAuthority()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await SeedEventsAsync(dbContext);
        var participantController = CreateEventsController(dbContext, seed.Participant);

        Assert.IsType<ForbidResult>(
            await participantController.AcceptEventAsync(seed.HiddenEventId, CancellationToken.None)
        );
        Assert.IsType<ForbidResult>(
            await participantController.MaybeEventAsync(seed.HiddenEventId, CancellationToken.None)
        );
        Assert.IsType<ForbidResult>(
            await participantController.RejectEventAsync(seed.HiddenEventId, CancellationToken.None)
        );
        Assert.False(await dbContext.UserEventMetas.AnyAsync(q => q.EventFk == seed.HiddenEventId));

        Assert.IsType<NoContentResult>(
            await participantController.AcceptEventAsync(seed.VisibleEventId, CancellationToken.None)
        );
        Assert.True(
            await dbContext.UserEventMetas.AnyAsync(
                q => q.EventFk == seed.VisibleEventId
                     && q.UserFk == seed.Participant.PrimaryKey
                     && q.Acceptance == EEventAcceptance.Accepted
            )
        );
    }

    [Fact]
    public async Task AcceptanceActions_AllowHiddenEventsForHostAndEventAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await SeedEventsAsync(dbContext);
        var hostController = CreateEventsController(dbContext, seed.Host);
        var adminController = CreateEventsController(
            dbContext,
            seed.Admin,
            (UtfClaims.Administrative.Event, string.Empty)
        );

        Assert.IsType<NoContentResult>(
            await hostController.AcceptEventAsync(seed.HiddenEventId, CancellationToken.None)
        );
        dbContext.ChangeTracker.Clear();
        Assert.IsType<NoContentResult>(
            await adminController.MaybeEventAsync(seed.HiddenEventId, CancellationToken.None)
        );

        Assert.True(
            await dbContext.UserEventMetas.AnyAsync(
                q => q.EventFk == seed.HiddenEventId
                     && q.UserFk == seed.Host.PrimaryKey
                     && q.Acceptance == EEventAcceptance.Accepted
            )
        );
        Assert.True(
            await dbContext.UserEventMetas.AnyAsync(
                q => q.EventFk == seed.HiddenEventId
                     && q.UserFk == seed.Admin.PrimaryKey
                     && q.Acceptance == EEventAcceptance.Maybe
            )
        );
    }

    [Fact]
    public async Task SelfSlotAssignment_RequiresVisibleEventAndVisibleSelfAssignableSlotForParticipants()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await SeedEventsAsync(dbContext);
        dbContext.EventSlots.AddRange(
            new EventSlot
            {
                EventFk          = seed.HiddenEventId,
                SlotNumber       = 1,
                IsSelfAssignable = true,
                IsVisible        = true,
                Group            = "Alpha",
                Title            = "Rifleman",
                Side             = EArmaSide.West,
            },
            new EventSlot
            {
                EventFk          = seed.VisibleEventId,
                SlotNumber       = 1,
                IsSelfAssignable = true,
                IsVisible        = false,
                Group            = "Alpha",
                Title            = "Hidden Rifleman",
                Side             = EArmaSide.West,
            },
            new EventSlot
            {
                EventFk          = seed.VisibleEventId,
                SlotNumber       = 2,
                IsSelfAssignable = true,
                IsVisible        = true,
                Group            = "Alpha",
                Title            = "Automatic Rifleman",
                Side             = EArmaSide.West,
            }
        );
        await dbContext.SaveChangesAsync();
        var participantController = CreateSlottingController(dbContext, seed.Participant);
        var hostController = CreateSlottingController(dbContext, seed.Host);

        Assert.IsType<ForbidResult>(
            await participantController.AssignEventSlotToSelf(seed.HiddenEventId, 1, CancellationToken.None)
        );
        Assert.IsType<ForbidResult>(
            await participantController.AssignEventSlotToSelf(seed.VisibleEventId, 1, CancellationToken.None)
        );
        Assert.IsType<NoContentResult>(
            await hostController.AssignEventSlotToSelf(seed.HiddenEventId, 1, CancellationToken.None)
        );
        Assert.IsType<NoContentResult>(
            await participantController.AssignEventSlotToSelf(seed.VisibleEventId, 2, CancellationToken.None)
        );

        var visibleSlot = await dbContext.EventSlots.SingleAsync(
            q => q.EventFk == seed.VisibleEventId && q.SlotNumber == 2
        );
        Assert.Equal(seed.Participant.PrimaryKey, visibleSlot.AssignedToFk);
    }

    [Fact]
    public async Task AllSlots_RequiresVisibleEventOrEventAuthority()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var seed = await SeedEventsAsync(dbContext);
        dbContext.EventSlots.AddRange(
            new EventSlot
            {
                EventFk          = seed.HiddenEventId,
                SlotNumber       = 1,
                AssignedToFk     = seed.Participant.PrimaryKey,
                IsSelfAssignable = true,
                IsVisible        = true,
                Group            = "Alpha",
                Title            = "Rifleman",
                Side             = EArmaSide.West,
            },
            new EventSlot
            {
                EventFk          = seed.HiddenEventId,
                SlotNumber       = 2,
                IsSelfAssignable = false,
                IsVisible        = false,
                Group            = "Command",
                Title            = "Zeus",
                Side             = EArmaSide.West,
            }
        );
        await dbContext.SaveChangesAsync();
        var anonymousController = CreateAnonymousSlottingController(dbContext);
        var participantController = CreateSlottingController(dbContext, seed.Participant);
        var hostController = CreateSlottingController(dbContext, seed.Host);
        var adminController = CreateSlottingController(
            dbContext,
            seed.Admin,
            (UtfClaims.Administrative.Event, string.Empty)
        );

        Assert.IsType<NoContentResult>(
            await anonymousController.AllAsync(seed.HiddenEventId, CancellationToken.None)
        );
        Assert.IsType<NoContentResult>(
            await participantController.AllAsync(seed.HiddenEventId, CancellationToken.None)
        );

        var hostSlots = AssertOkSlots(await hostController.AllAsync(seed.HiddenEventId, CancellationToken.None));
        Assert.Equal(2, hostSlots.Length);
        Assert.Contains(hostSlots, q => q.AssignedToFk == seed.Participant.PrimaryKey);

        var adminSlots = AssertOkSlots(await adminController.AllAsync(seed.HiddenEventId, CancellationToken.None));
        Assert.Equal(2, adminSlots.Length);
        Assert.Contains(adminSlots, q => q.AssignedToFk == seed.Participant.PrimaryKey);
    }

    private static async Task<EventSeed> SeedEventsAsync(DatabaseContext dbContext)
    {
        var host = CreateUser("event-host");
        var participant = CreateUser("participant");
        var admin = CreateUser("event-admin");
        var terrainImageAssetId = Guid.NewGuid();
        var terrain = new Terrain
        {
            PrimaryKey     = 1,
            Title          = "Altis",
            ImageAssetFk   = terrainImageAssetId,
            IsActive       = true,
        };
        var modPackDefinition = new ModPackDefinition
        {
            PrimaryKey       = 1,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(-2)),
            Title            = "Operations",
            OwnerFk          = host.PrimaryKey,
            IsActive         = true,
            IsComposition    = false,
        };
        var modPackRevision = new ModPackRevision
        {
            PrimaryKey       = 1,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(-1)),
            Html             = "<p>Operations</p>",
            UpdatedByFk      = host.PrimaryKey,
            IsActive         = true,
            DefinitionFk     = modPackDefinition.PrimaryKey,
        };
        var hiddenEvent = CreateEvent(host.PrimaryKey, terrain.PrimaryKey, modPackRevision.PrimaryKey, false);
        var visibleEvent = CreateEvent(host.PrimaryKey, terrain.PrimaryKey, modPackRevision.PrimaryKey, true);

        dbContext.Users.AddRange(host, participant, admin);
        dbContext.MediaAssets.AddRange(
            Api.Tests.MediaAssetTestData.Create(terrainImageAssetId),
            Api.Tests.MediaAssetTestData.Create(hiddenEvent.ImageAssetFk),
            Api.Tests.MediaAssetTestData.Create(visibleEvent.ImageAssetFk)
        );
        dbContext.Terrains.Add(terrain);
        dbContext.ModPackDefinitions.Add(modPackDefinition);
        dbContext.ModPackRevisions.Add(modPackRevision);
        dbContext.Events.AddRange(hiddenEvent, visibleEvent);
        await dbContext.SaveChangesAsync();

        return new EventSeed(host, participant, admin, hiddenEvent.PrimaryKey, visibleEvent.PrimaryKey);
    }

    private static Event CreateEvent(Guid hostId, long terrainId, long modPackRevisionId, bool isVisible)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return new Event
        {
            PrimaryKey           = Guid.NewGuid(),
            Title                = isVisible ? "Visible operation" : "Hidden operation",
            Description          = "Operation briefing",
            TerrainFk            = terrainId,
            ModPackRevisionFk    = modPackRevisionId,
            ImageAssetFk         = Guid.NewGuid(),
            ScheduledForOriginal = now.Plus(Duration.FromDays(7)),
            ScheduledFor         = now.Plus(Duration.FromDays(7)),
            TimeStampCreated     = now,
            IsVisible            = isVisible,
            MinimumAccepted      = 1,
            OwnerFk              = hostId,
            HostedByFk           = hostId,
        };
    }

    private static EventsController CreateEventsController(
        DatabaseContext dbContext,
        User user,
        params (string type, string value)[] claims)
        => new(
            NullLogger<EventsController>.Instance,
            dbContext,
            null,
            new NoOpUpdateStreamService(),
            new BaseUrl("https://api.example.test/", "https://client.example.test/"),
            new MediaAssetService(dbContext)
        )
        {
            ControllerContext = CreateControllerContext(CreatePrincipal(user, claims)),
        };

    private static EventsController CreateAnonymousEventsController(DatabaseContext dbContext)
        => new(
            NullLogger<EventsController>.Instance,
            dbContext,
            null,
            new NoOpUpdateStreamService(),
            new BaseUrl("https://api.example.test/", "https://client.example.test/"),
            new MediaAssetService(dbContext)
        )
        {
            ControllerContext = CreateControllerContext(new ClaimsPrincipal(new ClaimsIdentity())),
        };

    private static EventSlottingController CreateSlottingController(
        DatabaseContext dbContext,
        User user,
        params (string type, string value)[] claims)
        => new(
            NullLogger<EventSlottingController>.Instance,
            dbContext,
            new NoOpUpdateStreamService()
        )
        {
            ControllerContext = CreateControllerContext(CreatePrincipal(user, claims)),
        };

    private static EventSlottingController CreateAnonymousSlottingController(DatabaseContext dbContext)
        => new(
            NullLogger<EventSlottingController>.Instance,
            dbContext,
            new NoOpUpdateStreamService()
        )
        {
            ControllerContext = CreateControllerContext(new ClaimsPrincipal(new ClaimsIdentity())),
        };

    private static PlainEventSlotDto[] AssertOkSlots(IActionResult result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<PlainEventSlotDto[]>(okResult.Value);
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

    private sealed record EventSeed(
        User Host,
        User Participant,
        User Admin,
        Guid HiddenEventId,
        Guid VisibleEventId);

    private sealed class NoOpUpdateStreamService : IUpdateStreamService
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
