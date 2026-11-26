using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// MET-01 and MET-04 proof that the metrics endpoints return the current readings and the tier series to viewers of
/// the server, answer 404 to everyone else and 400 to every range value other than <c>1h</c>, <c>24h</c> and
/// <c>7d</c>. The tests run the real <see cref="GameServersController"/>, metrics service and hosting authorization
/// rules against in-memory SQLite.
/// </summary>
public sealed class ServerMetricsEndpointTests
{
    /// <summary>
    /// A viewer receives the readings the control plane decides for the server, with the stored values.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Viewer_ReceivesTheDecidedCurrentReadings()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext,
            seed.Deployment!,
            lastHeartbeat: now,
            processStartedAt: now - Duration.FromMinutes(10));
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            seed.Deployment!,
            worker.PrimaryKey,
            receivedAt: now,
            ServerMetricsTestData.Sample(
                seed.Deployment!.PrimaryKey,
                now.ToDateTimeOffset(),
                cpu: 42,
                used: 2_000,
                total: 8_000,
                players: 3,
                capacity: 16));

        var result = await CreateController(HostingTestData.ServerPrincipal(
                seed.User,
                seed.Server,
                Claims.ResourceBased.Server.View))
            .GetMetricsAsync(seed.Server.PrimaryKey, ServerMetricsTestData.Create(store.DbContext), CancellationToken.None);

        var dto = Assert.IsType<GameServerMetricsDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(seed.Server.PrimaryKey, dto.GameServerId);
        Assert.Equal(42, dto.CpuPercent);
        Assert.Null(dto.CpuUnavailableReason);
        Assert.Equal(2_000, dto.MemoryUsedBytes);
        Assert.Equal(8_000, dto.MemoryTotalBytes);
        Assert.Equal(3, dto.PlayerCount);
        Assert.Equal(16, dto.PlayerCapacity);
        Assert.Equal(45, dto.StaleAfterSeconds);
    }

    /// <summary>
    /// A user without the view claim of the server and an unknown server id both get 404, so the existence of the
    /// server is not disclosed, for the readings and for the history.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task NonViewerAndUnknownServer_GetNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var stranger = HostingTestData.Principal(CreateStranger());
        var viewerOfOther = HostingTestData.Principal(
            CreateStranger(),
            (Claims.ResourceBased.Server.View, Guid.NewGuid().ToString()));
        var unknownId = Guid.NewGuid();
        var viewerOfUnknown = HostingTestData.Principal(
            CreateStranger(),
            (Claims.ResourceBased.Server.View, unknownId.ToString()));

        var current = await CreateController(stranger)
            .GetMetricsAsync(seed.Server.PrimaryKey, service, CancellationToken.None);
        var history = await CreateController(viewerOfOther)
            .GetMetricsHistoryAsync(seed.Server.PrimaryKey, "1h", service, CancellationToken.None);
        var unknown = await CreateController(viewerOfUnknown)
            .GetMetricsAsync(unknownId, service, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(current);
        Assert.IsType<NotFoundObjectResult>(history);
        Assert.IsType<NotFoundObjectResult>(unknown);
    }

    /// <summary>
    /// Each accepted range reads the tier of its resolution: 15, 60 and 300 seconds.
    /// </summary>
    /// <param name="range">The range code sent by the client.</param>
    /// <param name="bucketSeconds">The resolution the tier is expected to carry.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData("1h", 15)]
    [InlineData("24h", 60)]
    [InlineData("7d", 300)]
    public async Task History_ReadsTheTierOfTheRange(string range, int bucketSeconds)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);

        var result = await CreateController(HostingTestData.ServerPrincipal(
                seed.User,
                seed.Server,
                Claims.ResourceBased.Server.View))
            .GetMetricsHistoryAsync(
                seed.Server.PrimaryKey,
                range,
                ServerMetricsTestData.Create(store.DbContext),
                CancellationToken.None);

        var dto = Assert.IsType<GameServerMetricsHistoryDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(bucketSeconds, dto.BucketSeconds);
        Assert.Equal(range, dto.Range);
        Assert.Equal(seed.Server.PrimaryKey, dto.GameServerId);
    }

    /// <summary>
    /// Any range value other than the three codes, including an empty and a missing value, is a 400.
    /// </summary>
    /// <param name="range">The rejected range value.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData("2h")]
    [InlineData("1H")]
    [InlineData("")]
    [InlineData(null)]
    public async Task History_RejectsAnyOtherRange(string? range)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);

        var result = await CreateController(HostingTestData.ServerPrincipal(
                seed.User,
                seed.Server,
                Claims.ResourceBased.Server.View))
            .GetMetricsHistoryAsync(
                seed.Server.PrimaryKey,
                range,
                ServerMetricsTestData.Create(store.DbContext),
                CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Creates a controller that needs none of its constructor services for the metrics actions.
    /// </summary>
    /// <param name="principal">The principal set as the request user.</param>
    /// <returns>The controller under test.</returns>
    private static GameServersController CreateController(ClaimsPrincipal principal)
        => new(null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    /// <summary>
    /// Creates an unsaved user who holds no claim.
    /// </summary>
    /// <returns>The user, used only to name a principal.</returns>
    private static User CreateStranger()
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = "stranger",
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord        = new DiscordUser { Username = "stranger" },
            Steam          = new SteamUser(),
        };
}
