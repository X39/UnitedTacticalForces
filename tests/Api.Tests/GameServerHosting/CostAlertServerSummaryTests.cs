using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-04 proof that the server projection carries the current cost owner's alert amounts and the manage flag for
/// viewers who may see that owner and discloses neither to any other viewer. The tests run the real
/// <c>GameServerProjectionService</c> and <c>HostingAuthorizationService</c> against in-memory SQLite.
/// </summary>
public sealed class CostAlertServerSummaryTests
{
    /// <summary>
    /// The owner's user sees the amounts ascending and may manage them, on the detail and in the fleet list.
    /// </summary>
    /// <returns>A task completing after both projections were verified.</returns>
    [Fact]
    public async Task OwnerUser_SeesAscendingAmountsAndMayManage()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SetCostAlertThresholdsAsync(
            store.DbContext, seed.CostOwner.PrimaryKey, true, true, 10m, 5m);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);

        var detail = await projection.GetServerAsync(seed.Server.PrimaryKey, principal);
        var list = Assert.Single(await projection.GetServersAsync(principal));

        foreach (var dto in new[] { detail, list })
        {
            Assert.NotNull(dto);
            Assert.Equal([5m, 10m], dto.CostAlertThresholds);
            Assert.True(dto.CanManageCostAlerts);
        }
    }

    /// <summary>
    /// A co:view holder with the server view claim sees the amounts but may not manage them.
    /// </summary>
    /// <returns>A task completing after the read-only visibility was verified.</returns>
    [Fact]
    public async Task OwnerViewer_SeesAmountsWithoutManage()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SetCostAlertThresholdsAsync(
            store.DbContext, seed.CostOwner.PrimaryKey, true, true, 5m, 10m);
        var viewer = AddUser(store, "viewer");
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.Principal(
            viewer,
            (Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString()),
            (Claims.ResourceBased.CostOwner.View, seed.CostOwner.PrimaryKey.ToString()));

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, principal);

        Assert.NotNull(dto);
        Assert.Equal([5m, 10m], dto.CostAlertThresholds);
        Assert.False(dto.CanManageCostAlerts);
    }

    /// <summary>
    /// A viewer with only the server view claim learns nothing about alert amounts or the manage right.
    /// </summary>
    /// <returns>A task completing after the hidden fields were verified.</returns>
    [Fact]
    public async Task ServerOnlyViewer_SeesNoAlertSummary()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SetCostAlertThresholdsAsync(store.DbContext, seed.CostOwner.PrimaryKey, true, true, 5m);
        var viewer = AddUser(store, "server-viewer");
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.ServerPrincipal(viewer, seed.Server, Claims.ResourceBased.Server.View);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);

        var detail = await projection.GetServerAsync(seed.Server.PrimaryKey, principal);
        var list = Assert.Single(await projection.GetServersAsync(principal));

        foreach (var dto in new[] { detail, list })
        {
            Assert.NotNull(dto);
            Assert.Null(dto.CostAlertThresholds);
            Assert.Null(dto.CanManageCostAlerts);
        }
    }

    /// <summary>
    /// An owner without a settings row projects an empty list, not null, for authorized viewers.
    /// </summary>
    /// <returns>A task completing after the empty list was verified.</returns>
    [Fact]
    public async Task OwnerWithoutSettings_ProjectsEmptyListForAuthorizedViewer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, principal);

        Assert.NotNull(dto);
        Assert.NotNull(dto.CostAlertThresholds);
        Assert.Empty(dto.CostAlertThresholds);
        Assert.True(dto.CanManageCostAlerts);
    }

    /// <summary>
    /// Adds an unsaved active user without a cost owner.
    /// </summary>
    /// <param name="store">The store the user is added to.</param>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>The added user.</returns>
    private static User AddUser(HostingTestStore store, string nickname)
    {
        var user = new User
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord        = new DiscordUser { Username = nickname },
            Steam          = new SteamUser(),
        };
        store.DbContext.Users.Add(user);
        return user;
    }
}
