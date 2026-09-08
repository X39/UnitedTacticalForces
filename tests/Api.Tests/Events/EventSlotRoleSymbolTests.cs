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
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Common.Event;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Eventing;
using Xunit;

namespace Api.Tests.Events;

/// <summary>
/// Verifies persistence and API projection behavior for event-slot role symbols.
/// </summary>
public sealed class EventSlotRoleSymbolTests
{
    /// <summary>
    /// Verifies that creating a slot persists an explicit role symbol and returns it in the documented DTO.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CreateEventSlot_PersistsExplicitRoleSymbolAndReturnsDtoAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var (host, appEvent) = await SeedEventAsync(dbContext);
        var controller = CreateController(dbContext, host);

        var result = await controller.CreateEventSlotAsync(
            appEvent.PrimaryKey,
            new EventSlotCreationPayload
            {
                Group = "Alpha",
                Title = "Combat medic",
                RoleSymbol = EEventSlotRoleSymbol.Medic,
                Side = EArmaSide.West,
                IsSelfAssignable = true,
                IsVisible = true,
            },
            CancellationToken.None);

        var dto = Assert.IsType<PlainEventSlotDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(1, dto.SlotNumber);
        Assert.Equal(EEventSlotRoleSymbol.Medic, dto.RoleSymbol);

        dbContext.ChangeTracker.Clear();
        var stored = await dbContext.EventSlots.SingleAsync();
        Assert.Equal(EEventSlotRoleSymbol.Medic, stored.RoleSymbol);
    }

    /// <summary>
    /// Verifies explicit updates, omitted-value preservation, and resetting a role symbol to automatic inference.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task UpdateEventSlot_UpdatesPreservesAndResetsRoleSymbolAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var (host, appEvent) = await SeedEventAsync(dbContext);
        await AddSlotAsync(dbContext, appEvent.PrimaryKey, EEventSlotRoleSymbol.Rifleman);
        var controller = CreateController(dbContext, host);

        Assert.IsType<NoContentResult>(await controller.UpdateEventSlotAsync(
            appEvent.PrimaryKey,
            1,
            new EventSlotUpdate { RoleSymbol = EEventSlotRoleSymbol.Grenadier },
            CancellationToken.None));
        dbContext.ChangeTracker.Clear();
        Assert.Equal(
            EEventSlotRoleSymbol.Grenadier,
            await dbContext.EventSlots.Select(q => q.RoleSymbol).SingleAsync());

        Assert.IsType<NoContentResult>(await controller.UpdateEventSlotAsync(
            appEvent.PrimaryKey,
            1,
            new EventSlotUpdate { Title = "Updated role title" },
            CancellationToken.None));
        dbContext.ChangeTracker.Clear();
        var preserved = await dbContext.EventSlots.SingleAsync();
        Assert.Equal("Updated role title", preserved.Title);
        Assert.Equal(EEventSlotRoleSymbol.Grenadier, preserved.RoleSymbol);

        Assert.IsType<NoContentResult>(await controller.UpdateEventSlotAsync(
            appEvent.PrimaryKey,
            1,
            new EventSlotUpdate { RoleSymbol = EEventSlotRoleSymbol.Automatic },
            CancellationToken.None));
        dbContext.ChangeTracker.Clear();
        Assert.Equal(
            EEventSlotRoleSymbol.Automatic,
            await dbContext.EventSlots.Select(q => q.RoleSymbol).SingleAsync());
    }

    /// <summary>
    /// Verifies that slot list projection includes the persisted role symbol.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AllSlots_ProjectsPersistedRoleSymbolAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var (host, appEvent) = await SeedEventAsync(dbContext);
        await AddSlotAsync(dbContext, appEvent.PrimaryKey, EEventSlotRoleSymbol.Pilot);
        dbContext.ChangeTracker.Clear();

        var result = await CreateController(dbContext, host)
            .AllAsync(appEvent.PrimaryKey, CancellationToken.None);

        var slots = Assert.IsType<PlainEventSlotDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        var slot = Assert.Single(slots);
        Assert.Equal(EEventSlotRoleSymbol.Pilot, slot.RoleSymbol);
    }

    /// <summary>
    /// Verifies that create and update operations reject undefined numeric role-symbol values.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task CreateAndUpdateEventSlot_RejectUndefinedRoleSymbolValuesAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var (host, appEvent) = await SeedEventAsync(dbContext);
        var controller = CreateController(dbContext, host);
        var undefinedRoleSymbol = (EEventSlotRoleSymbol) int.MaxValue;

        Assert.IsType<BadRequestResult>(await controller.CreateEventSlotAsync(
            appEvent.PrimaryKey,
            new EventSlotCreationPayload { RoleSymbol = undefinedRoleSymbol },
            CancellationToken.None));
        Assert.Empty(await dbContext.EventSlots.ToArrayAsync());

        await AddSlotAsync(dbContext, appEvent.PrimaryKey, EEventSlotRoleSymbol.Support);
        dbContext.ChangeTracker.Clear();
        Assert.IsType<BadRequestResult>(await controller.UpdateEventSlotAsync(
            appEvent.PrimaryKey,
            1,
            new EventSlotUpdate { RoleSymbol = undefinedRoleSymbol },
            CancellationToken.None));
        Assert.Equal(
            EEventSlotRoleSymbol.Support,
            await dbContext.EventSlots.Select(q => q.RoleSymbol).SingleAsync());
    }

    /// <summary>
    /// Adds a visible event slot with the supplied role symbol.
    /// </summary>
    /// <param name="dbContext">The database context to seed.</param>
    /// <param name="eventId">The event identifier for the slot.</param>
    /// <param name="roleSymbol">The role symbol to persist.</param>
    /// <returns>A task representing the asynchronous seed operation.</returns>
    private static async Task AddSlotAsync(
        DatabaseContext dbContext,
        Guid eventId,
        EEventSlotRoleSymbol roleSymbol)
    {
        dbContext.EventSlots.Add(new EventSlot
        {
            EventFk = eventId,
            SlotNumber = 1,
            Group = "Alpha",
            Title = "Rifleman",
            RoleSymbol = roleSymbol,
            Side = EArmaSide.West,
            IsSelfAssignable = true,
            IsVisible = true,
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a host and a visible event owned by that host.
    /// </summary>
    /// <param name="dbContext">The database context to seed.</param>
    /// <returns>The seeded host and event.</returns>
    private static async Task<(User Host, Event AppEvent)> SeedEventAsync(DatabaseContext dbContext)
    {
        var host = CreateUser("event-host");
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var appEvent = new Event
        {
            PrimaryKey = Guid.NewGuid(),
            Title = "Role-symbol operation",
            Description = "Operation briefing",
            ScheduledForOriginal = now.Plus(Duration.FromDays(7)),
            ScheduledFor = now.Plus(Duration.FromDays(7)),
            TimeStampCreated = now,
            IsVisible = true,
            MinimumAccepted = 1,
            OwnerFk = host.PrimaryKey,
            HostedByFk = host.PrimaryKey,
        };
        dbContext.Users.Add(host);
        dbContext.Events.Add(appEvent);
        await dbContext.SaveChangesAsync();
        return (host, appEvent);
    }

    /// <summary>
    /// Creates an authenticated event-slot controller for the supplied user.
    /// </summary>
    /// <param name="dbContext">The database context used by the controller.</param>
    /// <param name="user">The authenticated user.</param>
    /// <returns>The configured controller.</returns>
    private static EventSlottingController CreateController(DatabaseContext dbContext, User user)
        => new(
            NullLogger<EventSlottingController>.Instance,
            dbContext,
            new NoOpUpdateStreamService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(user),
                },
            },
        };

    /// <summary>
    /// Creates an authenticated principal for the supplied user.
    /// </summary>
    /// <param name="user">The user represented by the principal.</param>
    /// <returns>The authenticated principal.</returns>
    private static ClaimsPrincipal CreatePrincipal(User user)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new SecurityClaim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a persisted-user candidate with the required external-account records.
    /// </summary>
    /// <param name="nickname">The user's nickname.</param>
    /// <returns>The initialized user.</returns>
    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = nickname,
            AvatarMimeType = string.Empty,
            Discord = new DiscordUser { Username = nickname },
            Steam = new SteamUser(),
        };

    /// <summary>
    /// Creates the in-memory SQLite context used by a test.
    /// </summary>
    /// <param name="connection">The open SQLite connection that owns the in-memory database.</param>
    /// <returns>The initialized database context.</returns>
    private static async Task<DatabaseContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new NodaTimeSqliteApiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    /// <summary>
    /// Provides the update-stream dependency without producing side effects.
    /// </summary>
    private sealed class NoOpUpdateStreamService : IUpdateStreamService
    {
        /// <inheritdoc />
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask SendUpdateAsync<T>(string path, T package)
            => ValueTask.CompletedTask;
    }
}
