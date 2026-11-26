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
/// ACC-01 proof that the real game server list endpoint returns exactly the servers a member may view, and that
/// operate actions stay disabled for read-only (<c>sv:view</c>) holders. The tests run the real
/// <see cref="GameServersController" />, <see cref="GameServerProjectionService" /> and
/// <see cref="HostingAuthorizationService" /> against an in-memory SQLite store.
/// </summary>
public sealed class GameServerListAccessTests
{
    private const string OperatePermissionRequiredReason = "Server Operate permission is required.";

    /// <summary>
    /// A principal holding <c>sv</c> (all permissions) or <c>sv:view</c> bound to one server id receives that server
    /// and no other server.
    /// </summary>
    /// <param name="permission">The server claim type granted for the first server.</param>
    /// <returns>A task completing after the list contents were verified.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.All)]
    [InlineData(Claims.ResourceBased.Server.View)]
    public async Task GetAll_WithServerClaimBoundToOneServer_ReturnsOnlyThatServer(string permission)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext);
        var second = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(first.User, first.Server, permission);

        var servers = await GetAllAsync(store, principal);

        var projected = Assert.Single(servers);
        Assert.Equal(first.Server.PrimaryKey, projected.PrimaryKey);
        Assert.DoesNotContain(servers, q => q.PrimaryKey == second.Server.PrimaryKey);
    }

    /// <summary>
    /// Principals without a matching view grant, including bare claims whose value is empty, receive an empty
    /// list because the API matches resource claims only by server id.
    /// </summary>
    /// <param name="scenario">The name of the excluded principal scenario.</param>
    /// <returns>A task completing after the empty list was verified.</returns>
    [Theory]
    [InlineData("operate-only")]
    [InlineData("other-server")]
    [InlineData("no-claims")]
    [InlineData("bare-sv")]
    [InlineData("bare-sv-view")]
    public async Task GetAll_WithoutMatchingViewGrant_ReturnsEmptyList(string scenario)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = BuildExcludedPrincipal(scenario, first);

        var servers = await GetAllAsync(store, principal);

        Assert.Empty(servers);
    }

    /// <summary>
    /// A <c>sv:view</c> holder sees the start action as disabled with the operate-permission reason.
    /// </summary>
    /// <returns>A task completing after the projected start action was verified.</returns>
    [Fact]
    public async Task GetAll_WithViewClaim_DisablesStartActionWithOperateReason()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            first.User,
            first.Server,
            Claims.ResourceBased.Server.View);

        var start = FindStartAction(Assert.Single(await GetAllAsync(store, principal)));

        Assert.False(start.Enabled);
        Assert.Equal(OperatePermissionRequiredReason, start.UnavailableReason);
    }

    /// <summary>
    /// An <c>sv</c> holder, the all-permissions claim, sees the start action as enabled.
    /// </summary>
    /// <returns>A task completing after the projected start action was verified.</returns>
    [Fact]
    public async Task GetAll_WithAllPermissionsClaim_EnablesStartAction()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            first.User,
            first.Server,
            Claims.ResourceBased.Server.All);

        var start = FindStartAction(Assert.Single(await GetAllAsync(store, principal)));

        Assert.True(start.Enabled);
        Assert.Null(start.UnavailableReason);
    }

    /// <summary>
    /// Builds a principal that must not receive any server from the list endpoint.
    /// </summary>
    /// <param name="scenario">The scenario name used by the theory.</param>
    /// <param name="first">The seeded server the principal is nominally related to.</param>
    /// <returns>The principal for the requested scenario.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The scenario name is unknown.</exception>
    private static ClaimsPrincipal BuildExcludedPrincipal(string scenario, HostingTestSeed first)
        => scenario switch
        {
            "operate-only" => HostingTestData.ServerPrincipal(
                first.User,
                first.Server,
                Claims.ResourceBased.Server.Operate),
            "other-server" => HostingTestData.Principal(
                first.User,
                (Claims.ResourceBased.Server.View, Guid.NewGuid().ToString())),
            "no-claims" => HostingTestData.Principal(first.User),
            "bare-sv" => HostingTestData.Principal(first.User, (Claims.ResourceBased.Server.All, string.Empty)),
            "bare-sv-view" => HostingTestData.Principal(first.User, (Claims.ResourceBased.Server.View, string.Empty)),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario."),
        };

    /// <summary>
    /// Invokes the list endpoint on a real controller whose <c>HttpContext.User</c> is the given principal.
    /// </summary>
    /// <param name="store">The store holding the seeded servers.</param>
    /// <param name="principal">The authenticated principal making the request.</param>
    /// <returns>The server collection carried by the <see cref="OkObjectResult" />.</returns>
    private static async Task<IReadOnlyCollection<GameServerDto>> GetAllAsync(
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

        var result = await controller.GetAllAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<IReadOnlyCollection<GameServerDto>>(ok.Value);
    }

    /// <summary>
    /// Finds the projected start action of a server without regard to the identifier casing.
    /// </summary>
    /// <param name="server">The projected server.</param>
    /// <returns>The start action.</returns>
    private static GameServerActionDto FindStartAction(GameServerDto server)
        => Assert.Single(
            server.Actions,
            q => string.Equals(q.Id, GameServerActions.Start, StringComparison.OrdinalIgnoreCase));
}
