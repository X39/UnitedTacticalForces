using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// ACC-01 proof that the real notification endpoint reports <c>CanOperate</c> and the terminate cost-stop action
/// according to the member's server claims and omits servers the member may not view. The tests run the real
/// <see cref="GameServersController" />, <see cref="GameServerProjectionService" /> and
/// <see cref="HostingAuthorizationService" /> against an in-memory SQLite store.
/// </summary>
public sealed class GameServerNotificationAccessTests
{
    private const string OperatePermissionRequiredReason = "Server Operate permission is required.";

    /// <summary>
    /// <c>sv</c> grants operation and an enabled terminate action; <c>sv:view</c> yields a read-only notification
    /// whose terminate action is disabled with the operate-permission reason.
    /// </summary>
    /// <param name="permission">The server claim type granted for the first server.</param>
    /// <param name="expectedCanOperate">Whether the principal is expected to operate the server.</param>
    /// <returns>A task completing after the notification was verified.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.All, true)]
    [InlineData(Claims.ResourceBased.Server.View, false)]
    public async Task GetNotifications_ReportsCanOperateAndCostStopAction(
        string permission,
        bool expectedCanOperate)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var principal = HostingTestData.ServerPrincipal(first.User, first.Server, permission);

        var notifications = await GetNotificationsAsync(store, principal);

        var notification = Assert.Single(notifications);
        Assert.Equal(first.Server.PrimaryKey, notification.ServerId);
        Assert.Equal(expectedCanOperate, notification.CanOperate);
        var costStop = Assert.IsType<GameServerActionDto>(notification.CostStopAction);
        Assert.Equal(GameServerActions.Terminate, costStop.Id);
        Assert.Equal(expectedCanOperate, costStop.Enabled);
        if (!expectedCanOperate)
            Assert.Equal(OperatePermissionRequiredReason, costStop.UnavailableReason);
    }

    /// <summary>
    /// A principal whose view claim is bound to an unrelated server id receives no notification, so servers the
    /// principal may not view are omitted.
    /// </summary>
    /// <returns>A task completing after the empty notification list was verified.</returns>
    [Fact]
    public async Task GetNotifications_WithViewClaimForOtherServer_ReturnsEmptyList()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var principal = HostingTestData.Principal(
            first.User,
            (Claims.ResourceBased.Server.View, Guid.NewGuid().ToString()));

        var notifications = await GetNotificationsAsync(store, principal);

        Assert.Empty(notifications);
    }

    /// <summary>
    /// The notification of an <c>sv:view</c> holder carries the possible-cost signal of the active deployment so
    /// the client can join it with the server state.
    /// </summary>
    /// <returns>A task completing after the possible-cost signal was verified.</returns>
    [Fact]
    public async Task GetNotifications_WithViewClaim_ReportsPossibleCostOfActiveDeployment()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var principal = HostingTestData.ServerPrincipal(
            first.User,
            first.Server,
            Claims.ResourceBased.Server.View);

        var notification = Assert.Single(await GetNotificationsAsync(store, principal));

        Assert.True(notification.PossibleCost);
    }

    /// <summary>
    /// Invokes the notification endpoint on a real controller whose <c>HttpContext.User</c> is the given principal.
    /// </summary>
    /// <param name="store">The store holding the seeded servers.</param>
    /// <param name="principal">The authenticated principal making the request.</param>
    /// <returns>The notification collection carried by the <see cref="OkObjectResult" />.</returns>
    private static async Task<IReadOnlyCollection<GameServerNotificationDto>> GetNotificationsAsync(
        HostingTestStore store,
        ClaimsPrincipal principal)
    {
        var controller = new GameServersController(
            HostingTestData.CreateLifecycleService(store.DbContext),
            HostingTestData.CreateProjectionService(store.DbContext),
            new NoOpGameServerAccessService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

        var result = await controller.GetNotificationsAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<IReadOnlyCollection<GameServerNotificationDto>>(ok.Value);
    }
}
