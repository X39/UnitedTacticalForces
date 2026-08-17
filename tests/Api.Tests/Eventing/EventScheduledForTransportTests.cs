using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime.Serialization.SystemTextJson;
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
using Xunit;

namespace Api.Tests.Eventing;

public class EventScheduledForTransportTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    public async Task ApiController_HttpBindingAcceptsExplicitOffsetWithoutModelStateErrors(string action)
    {
        await using var app = await StartBindingProbeAsync();
        using var client = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.Single()),
        };
        using var content = new StringContent(
            """{ "scheduledFor": "2026-08-17T20:00:00+02:00" }""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync($"binding/events/{action}", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2026-08-17T20:00:00+02:00\"", responseBody);
        Assert.DoesNotContain("payload field is required", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("2026-08-17T18:00:00Z")]
    [InlineData("2026-08-17T20:00:00+02:00")]
    public async Task CreateEvent_JsonBindingAcceptsRfc3339OffsetsAndPersistsInstant(string scheduledFor)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser();
        dbContext.Users.Add(host);
        await dbContext.SaveChangesAsync();
        var json = $$"""
                     {
                       "title": "Offset operation",
                       "description": "Briefing",
                       "scheduledFor": "{{scheduledFor}}",
                       "hostedByFk": "{{host.PrimaryKey}}",
                       "isVisible": true,
                       "minimumAccepted": 0
                     }
                     """;

        var payload = JsonSerializer.Deserialize<EventCreationPayload>(json, JsonOptions);

        Assert.NotNull(payload);
        var result = await CreateController(dbContext, host).CreateEventAsync(payload, default);
        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(result).Value);
        var expected = Instant.FromUtc(2026, 8, 17, 18, 0);
        Assert.Equal(expected, created.ScheduledFor);
        Assert.Equal(expected, created.ScheduledForOriginal);
        var stored = await dbContext.Events.SingleAsync(q => q.PrimaryKey == created.PrimaryKey);
        Assert.Equal(expected, stored.ScheduledFor);
        Assert.Equal(expected, stored.ScheduledForOriginal);
        using var responseJson = JsonDocument.Parse(JsonSerializer.Serialize(created, JsonOptions));
        Assert.Equal("2026-08-17T18:00:00Z", responseJson.RootElement.GetProperty("scheduledFor").GetString());
    }

    [Theory]
    [InlineData("2026-08-17T18:00:00Z")]
    [InlineData("2026-08-17T20:00:00+02:00")]
    public async Task UpdateEvent_JsonBindingAcceptsRfc3339OffsetsAndPersistsInstant(string scheduledFor)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser();
        dbContext.Users.Add(host);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, host);
        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(
            await controller.CreateEventAsync(CreatePayload(host.PrimaryKey), default)).Value);
        var payload = JsonSerializer.Deserialize<EventUpdate>(
            $$"""{ "scheduledFor": "{{scheduledFor}}" }""",
            JsonOptions);

        Assert.NotNull(payload);
        Assert.IsType<NoContentResult>(await controller.UpdateEventAsync(created.PrimaryKey, payload, default));
        var stored = await dbContext.Events.SingleAsync(q => q.PrimaryKey == created.PrimaryKey);
        Assert.Equal(Instant.FromUtc(2026, 8, 17, 18, 0), stored.ScheduledFor);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"scheduledFor\": null }")]
    public async Task UpdateEvent_OmittedOrNullScheduleLeavesExistingInstantUnchanged(string json)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var host = CreateUser();
        dbContext.Users.Add(host);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, host);
        var created = Assert.IsType<PlainEventDto>(Assert.IsType<OkObjectResult>(
            await controller.CreateEventAsync(CreatePayload(host.PrimaryKey), default)).Value);
        var payload = JsonSerializer.Deserialize<EventUpdate>(json, JsonOptions);

        Assert.NotNull(payload);
        Assert.IsType<NoContentResult>(await controller.UpdateEventAsync(created.PrimaryKey, payload, default));
        var stored = await dbContext.Events.SingleAsync(q => q.PrimaryKey == created.PrimaryKey);
        Assert.Equal(created.ScheduledFor, stored.ScheduledFor);
    }

    private static EventCreationPayload CreatePayload(Guid hostId)
        => new()
        {
            Title = "Original operation",
            Description = "Briefing",
            ScheduledFor = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero),
            HostedByFk = hostId,
            IsVisible = true,
            MinimumAccepted = 0,
        };

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

    private static ClaimsPrincipal CreatePrincipal(User user)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new SecurityClaim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser()
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = "event-host",
            AvatarMimeType = string.Empty,
            Discord = new DiscordUser { Username = "event-host" },
            Steam = new SteamUser(),
        };

    private static async Task<DatabaseContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new NodaTimeSqliteApiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 64 };
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        return options;
    }

    private static async Task<WebApplication> StartBindingProbeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(EventScheduledForBindingProbeController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class NoopUpdateStreamService : IUpdateStreamService
    {
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask SendUpdateAsync<T>(string path, T package)
            => ValueTask.CompletedTask;
    }
}

[ApiController]
[Route("binding/events")]
public sealed class EventScheduledForBindingProbeController : ControllerBase
{
    [HttpPost("create")]
    public IActionResult Create([FromBody] EventCreationPayload payload)
        => Ok(payload.ScheduledFor);

    [HttpPost("update")]
    public IActionResult Update([FromBody] EventUpdate payload)
        => Ok(payload.ScheduledFor);
}
