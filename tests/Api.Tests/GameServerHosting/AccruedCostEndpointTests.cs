using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-03 proof that the accrued-cost endpoint returns the cost of the current or most recent session as the
/// control plane calculates it from charged ledger slices, never shows unknown as zero, and discloses no amount to a
/// viewer who may not view the cost owner. The tests run the real <see cref="GameServersController" />,
/// <see cref="CostAccrualService" /> and <see cref="HostingAuthorizationService" /> against in-memory SQLite.
/// </summary>
public sealed class AccruedCostEndpointTests
{
    /// <summary>
    /// The instant every seeded deployment starts at.
    /// </summary>
    private static readonly Instant Start = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// A slice charged for 15 minutes is prorated by its elapsed fraction while the deployment may still cost.
    /// </summary>
    /// <returns>A task completing after the prorated amount was verified.</returns>
    [Fact]
    public async Task ActiveSession_ProratesChargedSliceAtRequestInstant()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);
        var now = Start + Duration.FromMinutes(6);

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            now,
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(seed.Deployment!.PrimaryKey, session.DeploymentId);
        Assert.Equal(0.0100m, session.Amount);
        Assert.False(session.IsFinal);
        Assert.Equal(now, session.AsOf);
        Assert.Null(session.UnavailableReason);
        Assert.Equal("EUR", session.Currency);
    }

    /// <summary>
    /// Once the deployment is resolved the amount is clipped to the resolution instant and no longer advances.
    /// </summary>
    /// <returns>A task completing after the frozen amount was verified.</returns>
    [Fact]
    public async Task ResolvedSession_IsFinalAndFrozenAtResolutionInstant()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);
        var resolvedAt = Start + Duration.FromMinutes(9);
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampResolved = resolvedAt;
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var soon = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey, principal, resolvedAt + Duration.FromMinutes(1), CancellationToken.None);
        var later = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey, principal, resolvedAt + Duration.FromDays(3), CancellationToken.None);

        foreach (var session in new[] { soon.Session, later.Session })
        {
            Assert.NotNull(session);
            Assert.Equal(0.0150m, session.Amount);
            Assert.True(session.IsFinal);
            Assert.Equal(resolvedAt, session.AsOf);
        }
    }

    /// <summary>
    /// A deployment that may cost but has no charged slice reports amount null with the not-yet-charged reason, not
    /// zero.
    /// </summary>
    /// <returns>A task completing after the unavailable reason was verified.</returns>
    [Fact]
    public async Task ActiveSessionWithoutSlices_IsNotYetChargedNotZero()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.Deployment!.EstimatedHourlyCostEur = 0.10m;
        await store.DbContext.SaveChangesAsync();

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            Start + Duration.FromMinutes(1),
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Null(session.Amount);
        Assert.Equal(AccruedCostUnavailableReasons.NotYetCharged, session.UnavailableReason);
    }

    /// <summary>
    /// A deployment that may cost but has neither a slice nor an hourly rate reports the rate reason.
    /// </summary>
    /// <returns>A task completing after the rate reason was verified.</returns>
    [Fact]
    public async Task ActiveSessionWithoutSlicesAndRate_ReportsRateReason()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            Start + Duration.FromMinutes(1),
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Null(session.Amount);
        Assert.Equal(AccruedCostUnavailableReasons.Rate, session.UnavailableReason);
    }

    /// <summary>
    /// A server that never had a deployment has no session.
    /// </summary>
    /// <returns>A task completing after the missing session was verified.</returns>
    [Fact]
    public async Task ServerWithoutDeployment_HasNoSession()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            Start,
            CancellationToken.None);

        Assert.Equal(seed.Server.PrimaryKey, dto.GameServerId);
        Assert.Null(dto.Session);
    }

    /// <summary>
    /// A newer deployment that never cost does not hide the total of the earlier session that did.
    /// </summary>
    /// <returns>A task completing after the earlier session total was verified.</returns>
    [Fact]
    public async Task NeverCostLatestDeployment_KeepsPreviousSessionTotal()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);
        var resolvedAt = Start + Duration.FromMinutes(9);
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampResolved = resolvedAt;
        var attemptedAt = Start + Duration.FromHours(1);
        store.DbContext.GameServerDeployments.Add(new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            Status = EDeploymentStatus.Failed,
            Stage = "failed",
            PossibleCost = false,
            TimeStampCreated = attemptedAt,
            TimeStampUpdated = attemptedAt,
            TimeStampResolved = attemptedAt,
        });
        await store.DbContext.SaveChangesAsync();

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            attemptedAt + Duration.FromHours(1),
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(seed.Deployment.PrimaryKey, session.DeploymentId);
        Assert.Equal(0.0150m, session.Amount);
        Assert.True(session.IsFinal);
    }

    /// <summary>
    /// A viewer who holds only the server view permission, and neither owns nor may view the cost owner, learns no
    /// amount: the endpoint returns the hidden reason and no cost value.
    /// </summary>
    /// <returns>A task completing after the hidden session was verified.</returns>
    [Fact]
    public async Task ViewerWithoutCostOwnerVisibility_GetsHiddenWithoutAmount()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampResolved = Start + Duration.FromMinutes(9);
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.ServerPrincipal(CreateStranger(), seed.Server, Claims.ResourceBased.Server.View);

        var dto = await GetThroughControllerAsync(store, seed.Server.PrimaryKey, principal);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Null(session.Amount);
        Assert.Equal(AccruedCostUnavailableReasons.Hidden, session.UnavailableReason);
        Assert.Equal(seed.Deployment.PrimaryKey, session.DeploymentId);
        Assert.True(session.IsFinal);
    }

    /// <summary>
    /// A viewer who holds the cost-owner view claim for the session's owner receives the amount through the
    /// controller action.
    /// </summary>
    /// <returns>A task completing after the visible amount was verified.</returns>
    [Fact]
    public async Task ViewerWithCostOwnerViewClaim_GetsAmountThroughController()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampResolved = Start + Duration.FromMinutes(9);
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.Principal(
            CreateStranger(),
            (Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString()),
            (Claims.ResourceBased.CostOwner.View, seed.CostOwner.PrimaryKey.ToString()));

        var dto = await GetThroughControllerAsync(store, seed.Server.PrimaryKey, principal);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(0.0150m, session.Amount);
        Assert.Null(session.UnavailableReason);
    }

    /// <summary>
    /// A viewer without view permission on the server, and an unknown server id, both get 404 so the existence of
    /// the server is not disclosed.
    /// </summary>
    /// <returns>A task completing after both responses were verified.</returns>
    [Fact]
    public async Task UnviewableOrUnknownServer_ReturnsNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var withoutView = HostingTestData.Principal(CreateStranger());
        var admin = HostingTestData.Principal(CreateStranger(), (Claims.Administrative.Hosting, string.Empty));

        var unviewable = await CreateController(store, withoutView)
            .GetAccruedCostAsync(seed.Server.PrimaryKey, CreateService(store), CancellationToken.None);
        var unknown = await CreateController(store, admin)
            .GetAccruedCostAsync(Guid.NewGuid(), CreateService(store), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(unviewable);
        Assert.IsType<NotFoundObjectResult>(unknown);
    }

    /// <summary>
    /// A Hetzner session bills every started hour, so five minutes into its first slice it has already cost a full
    /// hour (hourly rate times one hour), and the amount is still advancing.
    /// </summary>
    /// <returns>A task completing after the full-hour amount was verified.</returns>
    [Fact]
    public async Task HetznerSessionAtMinuteFive_ReportsAFullHour()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext,
            seed.Deployment!,
            Start,
            Start + Duration.FromMinutes(15),
            0.0250m,
            HetznerRuntimeProvider.Identifier);
        var now = Start + Duration.FromMinutes(5);

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            now,
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(0.1000m, session.Amount);
        Assert.False(session.IsFinal);
        Assert.Equal(now, session.AsOf);
        Assert.Null(session.UnavailableReason);
    }

    /// <summary>
    /// The same slice charged by a provider that declares a per-started-second unit keeps its second granularity, so
    /// the full-hour rounding is a property of the provider and not of the calculator.
    /// </summary>
    /// <returns>A task completing after the per-second amount was verified.</returns>
    [Fact]
    public async Task PerSecondProviderSessionAtMinuteFive_KeepsItsOwnGranularity()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            Start + Duration.FromMinutes(5),
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(0.0083m, session.Amount);
    }

    /// <summary>
    /// A slice whose ledger provider identifier matches no registered provider has no known billing unit, so the
    /// amount is unavailable with the billing-unit reason and is never a lower prorated guess; a viewer without
    /// cost-owner visibility still gets the hidden reason.
    /// </summary>
    /// <returns>A task completing after both reasons were verified.</returns>
    [Fact]
    public async Task UnregisteredProvider_IsUnavailableWithBillingUnitReason()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext,
            seed.Deployment!,
            Start,
            Start + Duration.FromMinutes(15),
            0.0250m,
            "unregistered-cloud");
        var now = Start + Duration.FromMinutes(5);

        var visible = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            now,
            CancellationToken.None);
        var hidden = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(CreateStranger(), seed.Server, Claims.ResourceBased.Server.View),
            now,
            CancellationToken.None);

        var visibleSession = Assert.IsType<AccruedCostDto>(visible.Session);
        Assert.Null(visibleSession.Amount);
        Assert.Equal(AccruedCostUnavailableReasons.BillingUnit, visibleSession.UnavailableReason);
        var hiddenSession = Assert.IsType<AccruedCostDto>(hidden.Session);
        Assert.Null(hiddenSession.Amount);
        Assert.Equal(AccruedCostUnavailableReasons.Hidden, hiddenSession.UnavailableReason);
    }

    /// <summary>
    /// The controller action returns the same full-hour amount for a Hetzner session as the service.
    /// </summary>
    /// <returns>A task completing after the controller amount was verified.</returns>
    [Fact]
    public async Task HetznerSessionThroughController_ReportsAFullHour()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext,
            seed.Deployment!,
            Start,
            Start + Duration.FromMinutes(15),
            0.0250m,
            HetznerRuntimeProvider.Identifier);
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampResolved = Start + Duration.FromMinutes(5);
        await store.DbContext.SaveChangesAsync();
        var principal = HostingTestData.Principal(
            CreateStranger(),
            (Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString()),
            (Claims.ResourceBased.CostOwner.View, seed.CostOwner.PrimaryKey.ToString()));

        var dto = await GetThroughControllerAsync(store, seed.Server.PrimaryKey, principal);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(0.1000m, session.Amount);
        Assert.True(session.IsFinal);
    }

    /// <summary>
    /// A Hetzner session reports the one-hour billing unit and the end of the started hour, so the browser can say
    /// when the amount steps next; a hidden viewer and an unavailable amount receive neither fact.
    /// </summary>
    /// <returns>A task completing after the billing facts were verified.</returns>
    [Fact]
    public async Task HetznerSession_ReportsBillingUnitAndBilledThroughOnlyWithTheAmount()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext,
            seed.Deployment!,
            Start,
            Start + Duration.FromMinutes(15),
            0.0250m,
            HetznerRuntimeProvider.Identifier);
        var now = Start + Duration.FromMinutes(5);

        var visible = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            now,
            CancellationToken.None);
        var hidden = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(CreateStranger(), seed.Server, Claims.ResourceBased.Server.View),
            now,
            CancellationToken.None);

        var visibleSession = Assert.IsType<AccruedCostDto>(visible.Session);
        Assert.Equal(3600, visibleSession.BillingUnitSeconds);
        Assert.Equal(Start + Duration.FromHours(1), visibleSession.BilledThrough);
        var hiddenSession = Assert.IsType<AccruedCostDto>(hidden.Session);
        Assert.Null(hiddenSession.Amount);
        Assert.Null(hiddenSession.BillingUnitSeconds);
        Assert.Null(hiddenSession.BilledThrough);
    }

    /// <summary>
    /// A provider that declares a per-started-second unit reports a one second unit.
    /// </summary>
    /// <returns>A task completing after the unit length was verified.</returns>
    [Fact]
    public async Task PerSecondProviderSession_ReportsAOneSecondUnit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext, seed.Deployment!, Start, Start + Duration.FromMinutes(15), 0.0250m);

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            Start + Duration.FromMinutes(5),
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Equal(1, session.BillingUnitSeconds);
        Assert.Equal(Start + Duration.FromMinutes(5), session.BilledThrough);
    }

    /// <summary>
    /// An unavailable amount carries no billing facts.
    /// </summary>
    /// <returns>A task completing after the missing billing facts were verified.</returns>
    [Fact]
    public async Task UnavailableAmount_ReportsNoBillingFacts()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await HostingTestData.AddCostSliceAsync(
            store.DbContext,
            seed.Deployment!,
            Start,
            Start + Duration.FromMinutes(15),
            0.0250m,
            "unregistered-cloud");

        var dto = await CreateService(store).GetForServerAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View),
            Start + Duration.FromMinutes(5),
            CancellationToken.None);

        var session = Assert.IsType<AccruedCostDto>(dto.Session);
        Assert.Null(session.Amount);
        Assert.Null(session.BillingUnitSeconds);
        Assert.Null(session.BilledThrough);
    }

    /// <summary>
    /// Invokes the accrued-cost action on a real controller whose <c>HttpContext.User</c> is the given principal.
    /// </summary>
    /// <param name="store">The store holding the seeded server.</param>
    /// <param name="gameServerId">The server whose accrued cost is requested.</param>
    /// <param name="principal">The authenticated principal making the request.</param>
    /// <returns>The accrued cost carried by the <see cref="OkObjectResult" />.</returns>
    private static async Task<GameServerAccruedCostDto> GetThroughControllerAsync(
        HostingTestStore store,
        Guid gameServerId,
        ClaimsPrincipal principal)
    {
        var result = await CreateController(store, principal)
            .GetAccruedCostAsync(gameServerId, CreateService(store), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<GameServerAccruedCostDto>(ok.Value);
    }

    /// <summary>
    /// Creates a controller that needs none of its constructor services for the accrued-cost action.
    /// </summary>
    /// <param name="store">The store the request runs against; unused by the controller itself.</param>
    /// <param name="principal">The principal set as the request user.</param>
    /// <returns>The controller under test.</returns>
    private static GameServersController CreateController(HostingTestStore store, ClaimsPrincipal principal)
        => new(null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    /// <summary>
    /// Creates the accrual service under test on the store's database context.
    /// </summary>
    /// <param name="store">The test store whose context the service reads.</param>
    /// <returns>The service under test.</returns>
    private static CostAccrualService CreateService(HostingTestStore store)
        => new(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            [new TestRuntimeProvider(), new EmulatedHetznerRuntimeProvider()]);

    /// <summary>
    /// Creates an unsaved user who is neither the cost owner's user nor holds any claim.
    /// </summary>
    /// <returns>The user, used only to name a principal.</returns>
    private static User CreateStranger()
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = "stranger",
            AvatarMimeType = string.Empty,
            Claims = [],
            Roles = [],
            Discord = new DiscordUser { Username = "stranger" },
            Steam = new SteamUser(),
        };
}
