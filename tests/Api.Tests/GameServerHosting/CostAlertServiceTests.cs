using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-04 and D-08 proof that cost alerts are listed and dismissed per viewer under the cost-owner view rule, that an
/// invisible alert id is reported as not found, and that the set form of the view rule agrees with the per-owner
/// check for every claim class.
/// </summary>
public sealed class CostAlertServiceTests
{
    /// <summary>
    /// The instant the first seeded alert is created at.
    /// </summary>
    private static readonly Instant Start = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// The personal owner's user lists an alert with server name, threshold, accrued amount, currency and time; a newer
    /// alert is listed first.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task OwnersUser_ListsAlertsNewestFirstWithFullDetails()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var older = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5.1234m, Start);
        var newer = await AddAlertAsync(store, seed, seed.CostOwner, 10m, 10.5m, Start + Duration.FromMinutes(5));

        var listed = await CreateService(store).ListAsync(HostingTestData.Principal(seed.User), CancellationToken.None);

        Assert.Equal([newer.PrimaryKey, older.PrimaryKey], listed.Select(q => q.Id).ToArray());
        var dto = listed.Last();
        Assert.Equal(seed.Server.PrimaryKey, dto.ServerId);
        Assert.Equal("Integration server", dto.ServerName);
        Assert.Equal(seed.CostOwner.PrimaryKey, dto.CostOwnerId);
        Assert.Equal(5m, dto.ThresholdAmount);
        Assert.Equal(5.1234m, dto.AccruedAmount);
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(Start, dto.CreatedAt);
    }

    /// <summary>
    /// At most 50 alerts are returned, and they are the newest ones.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task List_ReturnsAtMostFiftyNewestAlerts()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        for (var i = 1; i <= 52; i++)
            await AddAlertAsync(store, seed, seed.CostOwner, i, i, Start + Duration.FromMinutes(i));

        var listed = await CreateService(store).ListAsync(HostingTestData.Principal(seed.User), CancellationToken.None);

        Assert.Equal(50, listed.Count);
        Assert.Equal(52m, listed.First().ThresholdAmount);
        Assert.Equal(3m, listed.Last().ThresholdAmount);
    }

    /// <summary>
    /// A user with only the server view claim lists nothing, while a user with fin:view-all and one with co:view for
    /// the owner list the alert.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task ListVisibility_FollowsCostOwnerViewRule()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var alert = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var serverViewer = await AddUserAsync(store, "server-viewer");
        var financeViewer = await AddUserAsync(store, "finance-viewer");
        var ownerViewer = await AddUserAsync(store, "owner-viewer");
        var service = CreateService(store);

        var serverOnly = await service.ListAsync(
            HostingTestData.ServerPrincipal(serverViewer, seed.Server, Claims.ResourceBased.Server.View),
            CancellationToken.None);
        var finance = await service.ListAsync(
            HostingTestData.Principal(financeViewer, (Claims.Finance.ViewAll, string.Empty)),
            CancellationToken.None);
        var owner = await service.ListAsync(
            HostingTestData.Principal(
                ownerViewer,
                (Claims.ResourceBased.CostOwner.View, seed.CostOwner.PrimaryKey.ToString())),
            CancellationToken.None);

        Assert.Empty(serverOnly);
        Assert.Equal(alert.PrimaryKey, Assert.Single(finance).Id);
        Assert.Equal(alert.PrimaryKey, Assert.Single(owner).Id);
    }

    /// <summary>
    /// A principal without a user id lists nothing even when it holds an all-owner view claim.
    /// </summary>
    /// <returns>A task completing once the assertion ran.</returns>
    [Fact]
    public async Task PrincipalWithoutUserId_ListsNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new System.Security.Claims.Claim(Claims.Finance.All, string.Empty));

        var listed = await CreateService(store).ListAsync(new ClaimsPrincipal(identity), CancellationToken.None);

        Assert.Empty(listed);
    }

    /// <summary>
    /// Alerts stay listed after the session ended, and an alert of another owner is not listed for the owner's user.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task EndedSessionAlert_StaysListed_OtherOwnersAlertIsHidden()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddExternalOwnerAsync(store, "Other owner");
        var own = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        await AddAlertAsync(store, seed, other, 6m, 6m, Start);
        var deployment = await store.DbContext.GameServerDeployments.SingleAsync();
        deployment.PossibleCost = false;
        deployment.TimeStampResolved = Start + Duration.FromHours(1);
        await store.DbContext.SaveChangesAsync();

        var listed = await CreateService(store).ListAsync(HostingTestData.Principal(seed.User), CancellationToken.None);

        Assert.Equal(own.PrimaryKey, Assert.Single(listed).Id);
    }

    /// <summary>
    /// Dismissing hides the alert for that user only, and repeating the dismissal succeeds without a second row.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Dismiss_IsPerViewerAndIdempotent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var alert = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var finance = await AddUserAsync(store, "finance-viewer");
        var ownerPrincipal = HostingTestData.Principal(seed.User);
        var financePrincipal = HostingTestData.Principal(finance, (Claims.Finance.All, string.Empty));
        var service = CreateService(store);

        await service.DismissAsync(alert.PrimaryKey, ownerPrincipal, CancellationToken.None);
        await service.DismissAsync(alert.PrimaryKey, ownerPrincipal, CancellationToken.None);

        Assert.Empty(await service.ListAsync(ownerPrincipal, CancellationToken.None));
        Assert.Equal(alert.PrimaryKey, Assert.Single(await service.ListAsync(financePrincipal, CancellationToken.None)).Id);
        var dismissal = Assert.Single(await store.DbContext.CostAlertDismissals.AsNoTracking().ToListAsync());
        Assert.Equal(seed.User.PrimaryKey, dismissal.UserFk);
    }

    /// <summary>
    /// Dismissing an alert of an owner the user cannot view, or an unknown id, throws the not-found exception and
    /// stores nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Dismiss_InvisibleOrUnknownAlert_ThrowsNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddExternalOwnerAsync(store, "Other owner");
        var foreign = await AddAlertAsync(store, seed, other, 5m, 5m, Start);
        var service = CreateService(store);
        var principal = HostingTestData.Principal(seed.User);

        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => service.DismissAsync(foreign.PrimaryKey, principal, CancellationToken.None));
        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => service.DismissAsync(Guid.NewGuid(), principal, CancellationToken.None));
        Assert.Empty(await store.DbContext.CostAlertDismissals.ToListAsync());
    }

    /// <summary>
    /// Dismissing all alerts of one server dismisses every visible undismissed alert of that server for the caller
    /// only, and leaves alerts of other servers, of invisible owners and of other viewers untouched.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_DismissesVisibleAlertsOfThatServerForCallerOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddExternalOwnerAsync(store, "Other owner");
        var first = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var second = await AddAlertAsync(store, seed, seed.CostOwner, 6m, 6m, Start);
        var foreign = await AddAlertAsync(store, seed, other, 7m, 7m, Start);
        var otherServer = await AddAlertOfOtherServerAsync(store, seed);
        var finance = await AddUserAsync(store, "finance-viewer");
        var ownerPrincipal = HostingTestData.Principal(seed.User);
        var financePrincipal = HostingTestData.Principal(finance, (Claims.Finance.All, string.Empty));
        var service = CreateService(store);

        await service.DismissAllForServerAsync(
            seed.Server.PrimaryKey,
            [first.PrimaryKey, second.PrimaryKey, foreign.PrimaryKey, otherServer.PrimaryKey],
            ownerPrincipal,
            CancellationToken.None);

        var ownerRemaining = await service.ListAsync(ownerPrincipal, CancellationToken.None);
        Assert.Equal(otherServer.PrimaryKey, Assert.Single(ownerRemaining).Id);
        var financeRemaining = await service.ListAsync(financePrincipal, CancellationToken.None);
        Assert.Equal(
            new[] { first.PrimaryKey, second.PrimaryKey, foreign.PrimaryKey, otherServer.PrimaryKey }.Order().ToArray(),
            financeRemaining.Select(q => q.Id).Order().ToArray());
        Assert.Equal(2, await store.DbContext.CostAlertDismissals.CountAsync());
    }

    /// <summary>
    /// Dismissing all alerts of a server with nothing visible completes without error and stores nothing.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_WithNothingVisible_Succeeds()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var stranger = await AddUserAsync(store, "stranger");

        var alert = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);

        await CreateService(store).DismissAllForServerAsync(
            seed.Server.PrimaryKey,
            [alert.PrimaryKey],
            HostingTestData.Principal(stranger),
            CancellationToken.None);

        Assert.Empty(await store.DbContext.CostAlertDismissals.ToListAsync());
    }

    /// <summary>
    /// Dismissing one alert pushes one dismissed frame to the dismissing user only, and repeating the dismissal pushes
    /// nothing because no new dismissal was stored.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Dismiss_PublishesOneFrameToCallerOnly_AndNothingWhenAlreadyDismissed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var alert = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var publisher = new RecordingPublisher();
        var service = CreateService(store, publisher);
        var principal = HostingTestData.Principal(seed.User);

        await service.DismissAsync(alert.PrimaryKey, principal, CancellationToken.None);
        await service.DismissAsync(alert.PrimaryKey, principal, CancellationToken.None);

        var published = Assert.Single(publisher.Dismissed);
        Assert.Equal(seed.User.PrimaryKey, published.UserId);
        Assert.Equal([alert.PrimaryKey], published.AlertIds);
    }

    /// <summary>
    /// Dismissing all alerts of a server pushes one dismissed frame per newly dismissed alert, to the caller only.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_PublishesOneFramePerDismissedAlertToCallerOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var first = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var second = await AddAlertAsync(store, seed, seed.CostOwner, 6m, 6m, Start);
        var publisher = new RecordingPublisher();
        var service = CreateService(store, publisher);
        var principal = HostingTestData.Principal(seed.User);

        var ids = new[] { first.PrimaryKey, second.PrimaryKey };

        await service.DismissAllForServerAsync(seed.Server.PrimaryKey, ids, principal, CancellationToken.None);
        await service.DismissAllForServerAsync(seed.Server.PrimaryKey, ids, principal, CancellationToken.None);

        var published = Assert.Single(publisher.Dismissed);
        Assert.Equal(seed.User.PrimaryKey, published.UserId);
        Assert.Equal(ids.Order().ToArray(), published.AlertIds.Order().ToArray());
    }

    /// <summary>
    /// D-17: an alert raised after the viewer's list was loaded is not in the submitted ids, so dismissing all stays
    /// away from it; it is still listed and only the shown alert is dismissed and published.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_DismissesOnlyTheSubmittedIds()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var shown = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var raisedLater = await AddAlertAsync(store, seed, seed.CostOwner, 6m, 6m, Start + Duration.FromMinutes(1));
        var publisher = new RecordingPublisher();
        var service = CreateService(store, publisher);
        var principal = HostingTestData.Principal(seed.User);

        await service.DismissAllForServerAsync(
            seed.Server.PrimaryKey,
            [shown.PrimaryKey],
            principal,
            CancellationToken.None);

        Assert.Equal(raisedLater.PrimaryKey, Assert.Single(await service.ListAsync(principal, CancellationToken.None)).Id);
        var published = Assert.Single(publisher.Dismissed);
        Assert.Equal(seed.User.PrimaryKey, published.UserId);
        Assert.Equal([shown.PrimaryKey], published.AlertIds);
        Assert.Equal(shown.PrimaryKey, Assert.Single(await store.DbContext.CostAlertDismissals.ToListAsync()).CostAlertFk);
    }

    /// <summary>
    /// A submitted id of an alert that belongs to another server is ignored and the alert stays undismissed.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_IgnoresIdOfAnotherServer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var own = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var otherServer = await AddAlertOfOtherServerAsync(store, seed);
        var service = CreateService(store);
        var principal = HostingTestData.Principal(seed.User);

        await service.DismissAllForServerAsync(
            seed.Server.PrimaryKey,
            [own.PrimaryKey, otherServer.PrimaryKey],
            principal,
            CancellationToken.None);

        Assert.Equal(otherServer.PrimaryKey, Assert.Single(await service.ListAsync(principal, CancellationToken.None)).Id);
        Assert.Equal(own.PrimaryKey, Assert.Single(await store.DbContext.CostAlertDismissals.ToListAsync()).CostAlertFk);
    }

    /// <summary>
    /// A submitted id of an alert whose cost owner the caller may not view is ignored: the call succeeds, stores no
    /// dismissal and publishes nothing, so the answer does not reveal whether that alert exists.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_IgnoresIdOfUnviewableOwner_WithoutDisclosure()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddExternalOwnerAsync(store, "Other owner");
        var foreign = await AddAlertAsync(store, seed, other, 5m, 5m, Start);
        var publisher = new RecordingPublisher();
        var service = CreateService(store, publisher);
        var principal = HostingTestData.Principal(seed.User);

        await service.DismissAllForServerAsync(
            seed.Server.PrimaryKey,
            [foreign.PrimaryKey, Guid.NewGuid()],
            principal,
            CancellationToken.None);

        Assert.Empty(await store.DbContext.CostAlertDismissals.ToListAsync());
        Assert.Empty(publisher.Dismissed);
        var finance = await AddUserAsync(store, "finance-viewer");
        var financePrincipal = HostingTestData.Principal(finance, (Claims.Finance.All, string.Empty));
        Assert.Equal(foreign.PrimaryKey, Assert.Single(await service.ListAsync(financePrincipal, CancellationToken.None)).Id);
    }

    /// <summary>
    /// Repeating the call with the same ids adds no dismissal row and publishes no second frame.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task DismissAllForServer_RepeatedWithSameIds_IsIdempotent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var alert = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var publisher = new RecordingPublisher();
        var service = CreateService(store, publisher);
        var principal = HostingTestData.Principal(seed.User);

        await service.DismissAllForServerAsync(seed.Server.PrimaryKey, [alert.PrimaryKey], principal, CancellationToken.None);
        await service.DismissAllForServerAsync(seed.Server.PrimaryKey, [alert.PrimaryKey], principal, CancellationToken.None);

        Assert.Single(await store.DbContext.CostAlertDismissals.ToListAsync());
        Assert.Single(publisher.Dismissed);
    }

    /// <summary>
    /// An empty id list and a list of 51 ids are rejected with the validation exception the controller maps to 400,
    /// and nothing is stored.
    /// </summary>
    /// <param name="count">The number of ids submitted.</param>
    /// <returns>A task completing once the assertions ran.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task DismissAllForServer_RejectsEmptyOrOversizedIdList(int count)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

        await Assert.ThrowsAsync<HostingValidationException>(
            () => CreateService(store).DismissAllForServerAsync(
                seed.Server.PrimaryKey,
                ids,
                HostingTestData.Principal(seed.User),
                CancellationToken.None));

        Assert.Empty(await store.DbContext.CostAlertDismissals.ToListAsync());
    }

    /// <summary>
    /// For every claim class the set of viewable owners agrees with the per-owner check for owner A, owner B and the
    /// caller's personal owner.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task ViewableOwnerIds_AgreeWithCanViewCostOwnerForEveryClaimClass()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var ownerA = await AddExternalOwnerAsync(store, "A");
        var ownerB = await AddExternalOwnerAsync(store, "B");
        var ownUser = await AddUserAsync(store, "own-user");
        var ownOwner = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = ECostOwnerKind.User,
            UserFk           = ownUser.PrimaryKey,
            DisplayName      = "own",
            Currency         = "EUR",
            TimeStampCreated = Start,
            TimeStampUpdated = Start,
        };
        store.DbContext.CostOwners.Add(ownOwner);
        await store.DbContext.SaveChangesAsync();
        var a = ownerA.PrimaryKey.ToString();
        var rows = new (string Name, ClaimsPrincipal Principal)[]
        {
            ("hosting admin", HostingTestData.Principal(ownUser, (Claims.Administrative.Hosting, string.Empty))),
            ("empty fin", HostingTestData.Principal(ownUser, (Claims.Finance.All, string.Empty))),
            ("empty fin:view-all", HostingTestData.Principal(ownUser, (Claims.Finance.ViewAll, string.Empty))),
            ("co:view A", HostingTestData.Principal(ownUser, (Claims.ResourceBased.CostOwner.View, a))),
            ("co:view upper A", HostingTestData.Principal(ownUser, (Claims.ResourceBased.CostOwner.View, a.ToUpperInvariant()))),
            ("co:use A", HostingTestData.Principal(ownUser, (Claims.ResourceBased.CostOwner.Use, a))),
            ("non-empty fin", HostingTestData.Principal(ownUser, (Claims.Finance.All, "x"))),
            ("own owner", HostingTestData.Principal(ownUser)),
            ("unrelated", HostingTestData.Principal(await AddUserAsync(store, "unrelated"))),
            ("no user id", new ClaimsPrincipal(new ClaimsIdentity([new System.Security.Claims.Claim(Claims.ResourceBased.CostOwner.View, a)], "test"))),
        };
        var service = new HostingAuthorizationService(store.DbContext);
        var owners = new[] { ownerA.PrimaryKey, ownerB.PrimaryKey, ownOwner.PrimaryKey };

        foreach (var (name, principal) in rows)
        {
            var viewable = await service.GetViewableCostOwnerIdsAsync(principal);
            foreach (var owner in owners)
            {
                var expected = await service.CanViewCostOwnerAsync(principal, owner);
                var actual = viewable is null || viewable.Contains(owner);
                Assert.True(expected == actual, $"Row '{name}' on owner {owner}: check={expected}, set={actual}.");
            }
        }
    }

    /// <summary>
    /// Creates the service under test with a null logger.
    /// </summary>
    /// <param name="store">The store whose context the service uses.</param>
    /// <param name="publisher">The publisher double that records dismissal frames, or <see langword="null"/>.</param>
    /// <returns>The service under test.</returns>
    private static CostAlertService CreateService(HostingTestStore store, RecordingPublisher? publisher = null)
        => new(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            publisher ?? new RecordingPublisher(),
            NullLogger<CostAlertService>.Instance);

    /// <summary>
    /// Adds and saves an active user without claims.
    /// </summary>
    /// <param name="store">The store the user is added to.</param>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>A task resolving to the saved user.</returns>
    private static async Task<User> AddUserAsync(HostingTestStore store, string nickname)
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
        await store.DbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Adds and saves an external-organization cost owner.
    /// </summary>
    /// <param name="store">The store the owner is added to.</param>
    /// <param name="displayName">The display name of the owner.</param>
    /// <returns>A task resolving to the saved owner.</returns>
    private static async Task<CostOwner> AddExternalOwnerAsync(HostingTestStore store, string displayName)
    {
        var owner = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = ECostOwnerKind.ExternalOrganization,
            DisplayName      = displayName,
            Currency         = "EUR",
            TimeStampCreated = Start,
            TimeStampUpdated = Start,
        };
        store.DbContext.CostOwners.Add(owner);
        await store.DbContext.SaveChangesAsync();
        return owner;
    }

    /// <summary>
    /// Adds and saves one alert for the seeded deployment.
    /// </summary>
    /// <param name="store">The store the alert is added to.</param>
    /// <param name="seed">The seeded server and deployment.</param>
    /// <param name="owner">The cost owner the alert belongs to.</param>
    /// <param name="threshold">The threshold amount; unique per deployment.</param>
    /// <param name="accrued">The accrued amount at the crossing.</param>
    /// <param name="createdAt">The creation instant of the alert.</param>
    /// <returns>A task resolving to the saved alert.</returns>
    private static async Task<CostAlert> AddAlertAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        CostOwner owner,
        decimal threshold,
        decimal accrued,
        Instant createdAt)
    {
        var alert = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = seed.Deployment!.PrimaryKey,
            GameServerFk     = seed.Server.PrimaryKey,
            CostOwnerFk      = owner.PrimaryKey,
            ThresholdAmount  = threshold,
            AccruedAmount    = accrued,
            Currency         = "EUR",
            TimeStampCreated = createdAt,
        };
        store.DbContext.CostAlerts.Add(alert);
        await store.DbContext.SaveChangesAsync();
        return alert;
    }

    /// <summary>
    /// Adds a second server of the same owner with its own deployment and one alert.
    /// </summary>
    /// <param name="store">The store the rows are added to.</param>
    /// <param name="seed">The seeded first server whose owner and creator are reused.</param>
    /// <returns>A task resolving to the saved alert of the second server.</returns>
    private static async Task<CostAlert> AddAlertOfOtherServerAsync(HostingTestStore store, HostingTestSeed seed)
    {
        var server = new GameServer
        {
            PrimaryKey                = Guid.NewGuid(),
            Title                     = "Second server",
            GameIdentifier            = HostingTestData.GameIdentifier,
            RuntimeProviderIdentifier = TestRuntimeProvider.Identifier,
            CreatedByFk               = seed.User.PrimaryKey,
            CurrentCostOwnerFk        = seed.CostOwner.PrimaryKey,
            TimeStampCreated          = Start,
            TimeStampUpdated          = Start,
        };
        var deployment = new GameServerDeployment
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = server.PrimaryKey,
            CostOwnerFk      = seed.CostOwner.PrimaryKey,
            Status           = EDeploymentStatus.Running,
            Stage            = "running",
            PossibleCost     = true,
            TimeStampCreated = Start,
            TimeStampUpdated = Start,
            TimeStampStarted = Start,
        };
        var alert = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = deployment.PrimaryKey,
            GameServerFk     = server.PrimaryKey,
            CostOwnerFk      = seed.CostOwner.PrimaryKey,
            ThresholdAmount  = 5m,
            AccruedAmount    = 5m,
            Currency         = "EUR",
            TimeStampCreated = Start,
        };
        store.DbContext.GameServers.Add(server);
        store.DbContext.GameServerDeployments.Add(deployment);
        store.DbContext.CostAlerts.Add(alert);
        await store.DbContext.SaveChangesAsync();
        return alert;
    }

    /// <summary>
    /// An AutoStop alert is listed while its pending stop is pending and leaves the list once the stop resolved,
    /// while threshold alerts are unaffected.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task List_ExcludesAutoStopAlertOnceItsPendingStopResolved()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var threshold = await AddAlertAsync(store, seed, seed.CostOwner, 5m, 5m, Start);
        var (autoStop, pending) = await AddAutoStopAlertAsync(store, seed, Start + Duration.FromMinutes(1));
        var service = CreateService(store);
        var principal = HostingTestData.Principal(seed.User);

        var whilePending = await service.ListAsync(principal, CancellationToken.None);
        pending.Status = EAutoStopStatus.Cancelled;
        await store.DbContext.SaveChangesAsync();
        var afterResolved = await service.ListAsync(principal, CancellationToken.None);

        Assert.Equal([autoStop.PrimaryKey, threshold.PrimaryKey], whilePending.Select(q => q.Id).ToArray());
        Assert.Equal([threshold.PrimaryKey], afterResolved.Select(q => q.Id).ToArray());
    }

    /// <summary>
    /// Dismissing an AutoStop alert hides it only for the dismissing viewer and leaves the pending stop untouched;
    /// another holder of the same cost owner still sees it (group audience assumption).
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Dismiss_AutoStopAlert_HidesItForThatViewerOnlyAndKeepsStopPending()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var (autoStop, pending) = await AddAutoStopAlertAsync(store, seed, Start);
        var otherHolder = await AddUserAsync(store, "other-holder");
        var financePrincipal = HostingTestData.Principal(
            otherHolder,
            (Claims.Finance.ViewAll, string.Empty));
        var service = CreateService(store);

        await service.DismissAsync(autoStop.PrimaryKey, HostingTestData.Principal(seed.User), CancellationToken.None);

        Assert.Empty(await service.ListAsync(HostingTestData.Principal(seed.User), CancellationToken.None));
        Assert.Equal(
            [autoStop.PrimaryKey],
            (await service.ListAsync(financePrincipal, CancellationToken.None)).Select(q => q.Id).ToArray());
        var stored = await store.DbContext.Set<AutomationPendingStop>()
            .AsNoTracking()
            .SingleAsync(q => q.PrimaryKey == pending.PrimaryKey);
        Assert.Equal(EAutoStopStatus.Pending, stored.Status);
        Assert.Null(stored.ResolvedAt);
    }

    /// <summary>
    /// Adds and saves an AutoStop alert with its pending stop for the seeded deployment.
    /// </summary>
    /// <param name="store">The store the rows are added to.</param>
    /// <param name="seed">The seeded server and deployment.</param>
    /// <param name="createdAt">The creation instant of the alert.</param>
    /// <returns>A task resolving to the saved alert and its pending stop.</returns>
    private static async Task<(CostAlert Alert, AutomationPendingStop Pending)> AddAutoStopAlertAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        Instant createdAt)
    {
        var pending = await AutoStopAlertWriterTests.AddPendingStopAsync(store, seed, EAutoStopReason.Idle, 30);
        var alert = new CostAlert
        {
            PrimaryKey       = Guid.NewGuid(),
            DeploymentFk     = seed.Deployment!.PrimaryKey,
            GameServerFk     = seed.Server.PrimaryKey,
            CostOwnerFk      = seed.CostOwner.PrimaryKey,
            Kind             = ECostAlertKind.AutoStop,
            PendingStopFk    = pending.PrimaryKey,
            ThresholdAmount  = 0m,
            AccruedAmount    = 0m,
            Currency         = "EUR",
            TimeStampCreated = createdAt,
        };
        store.DbContext.CostAlerts.Add(alert);
        await store.DbContext.SaveChangesAsync();
        return (alert, pending);
    }

    /// <summary>
    /// A publisher double that records dismissal frames and ignores everything else.
    /// </summary>
    private sealed class RecordingPublisher : ICostAlertPublisher
    {
        /// <summary>
        /// Gets the recorded dismissal publications.
        /// </summary>
        /// <value>One entry per <see cref="PublishDismissedAsync"/> call.</value>
        public List<(Guid UserId, Guid[] AlertIds)> Dismissed { get; } = [];

        /// <inheritdoc />
        public Task PublishAccrualsAsync(IReadOnlyList<DeploymentAccrual> accruals, CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishRaisedAsync(IReadOnlyList<RaisedCostAlert> alerts, CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishDismissedAsync(
            Guid userId,
            IReadOnlyCollection<Guid> costAlertIds,
            CancellationToken cancellationToken)
        {
            Dismissed.Add((userId, costAlertIds.ToArray()));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task PublishResolvedAsync(IReadOnlyList<ResolvedCostAlert> alerts, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
