using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerAccessServiceTests
{
    [Fact]
    public void AllowedPermissions_AreTheExactServerDelegationAllowlist()
    {
        var service = new GameServerAccessService(null!, null!);

        Assert.Equal(
            [
                Claims.ResourceBased.Server.All,
                Claims.ResourceBased.Server.View,
                Claims.ResourceBased.Server.Operate,
                Claims.ResourceBased.Server.Modify,
                Claims.ResourceBased.Server.ManageAccess,
                Claims.ResourceBased.Server.DeleteData,
                Claims.ResourceBased.Server.ManageSecrets,
                Claims.ResourceBased.Server.RevealSecrets,
            ],
            service.AllowedPermissions);
    }

    /// <summary>
    /// Pins the shared search bound. The public service docs, the controller docs and the OpenAPI snapshot state the
    /// literal value, so changing the constant requires updating those descriptions together with this assertion.
    /// </summary>
    [Fact]
    public void HostingSearchLimits_MaximumTake_MatchesTheDocumentedBound()
    {
        Assert.Equal(50, HostingSearchLimits.MaximumTake);
    }

    [Fact]
    public async Task UserGrantAndRevoke_AreIdempotentListedAndAuditedOncePerChange()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("direct-user");
        store.DbContext.Users.Add(target);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var principal = AccessPrincipal(seed);

        await service.GrantUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            principal);
        await service.GrantUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            principal);

        var listed = await service.GetAsync(seed.Server.PrimaryKey, principal);
        var listedUser = Assert.Single(listed.Users);
        Assert.Equal(target.PrimaryKey, listedUser.UserId);
        Assert.Equal([Claims.ResourceBased.Server.Operate], listedUser.Permissions);
        Assert.Single(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.AccessGranted)
            .ToArrayAsync());

        await service.RevokeUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            principal);
        await service.RevokeUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            principal);

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.Users
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == target.PrimaryKey);
        Assert.DoesNotContain(persisted.Claims!, q =>
            q.Identifier == Claims.ResourceBased.Server.Operate
            && q.Value == seed.Server.PrimaryKey.ToString("D")
            && q.ValueType == null
            && !q.IsPrefix);
        Assert.Single(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.AccessRevoked)
            .ToArrayAsync());
    }

    [Fact]
    public async Task RoleGrantAndRevoke_AreIdempotentListedAndAuditedOncePerChange()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var role = new Role
        {
            Title = "Server operators",
            Description = "Scoped server operation",
            Claims = [],
            Users = [],
        };
        store.DbContext.Roles.Add(role);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var principal = AccessPrincipal(seed);

        await service.GrantRoleAsync(
            seed.Server.PrimaryKey,
            role.PrimaryKey,
            Claims.ResourceBased.Server.View,
            principal);
        await service.GrantRoleAsync(
            seed.Server.PrimaryKey,
            role.PrimaryKey,
            Claims.ResourceBased.Server.View,
            principal);

        var listed = await service.GetAsync(seed.Server.PrimaryKey, principal);
        var listedRole = Assert.Single(listed.Roles);
        Assert.Equal(role.PrimaryKey, listedRole.RoleId);
        Assert.Equal([Claims.ResourceBased.Server.View], listedRole.Permissions);
        Assert.Single(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.AccessGranted)
            .ToArrayAsync());

        await service.RevokeRoleAsync(
            seed.Server.PrimaryKey,
            role.PrimaryKey,
            Claims.ResourceBased.Server.View,
            principal);
        await service.RevokeRoleAsync(
            seed.Server.PrimaryKey,
            role.PrimaryKey,
            Claims.ResourceBased.Server.View,
            principal);

        Assert.Single(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.AccessRevoked)
            .ToArrayAsync());
    }

    [Fact]
    public async Task List_ReturnsOnlyExactIdentifierValueNullValueTypeNonPrefixGrants()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var serverValue = seed.Server.PrimaryKey.ToString("D");
        var user = CreateUser("filtered-user");
        user.Claims =
        [
            Grant(Claims.ResourceBased.Server.View, serverValue),
            Grant(Claims.ResourceBased.Server.Operate + ":child", serverValue),
            Grant(Claims.ResourceBased.Server.Modify, serverValue, valueType: "custom"),
            Grant(Claims.ResourceBased.Server.DeleteData, Guid.NewGuid().ToString("D")),
            Grant(Claims.ResourceBased.Server.ManageAccess, serverValue, isPrefix: true),
        ];
        var role = new Role
        {
            Title = "Exact role",
            Claims = [Grant(Claims.ResourceBased.Server.Operate, serverValue)],
            Users = [],
        };
        store.DbContext.AddRange(user, role);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var result = await service.GetAsync(seed.Server.PrimaryKey, AccessPrincipal(seed));

        var listedUser = Assert.Single(result.Users);
        Assert.Equal(user.PrimaryKey, listedUser.UserId);
        Assert.Equal([Claims.ResourceBased.Server.View], listedUser.Permissions);
        var listedRole = Assert.Single(result.Roles);
        Assert.Equal(role.PrimaryKey, listedRole.RoleId);
        Assert.Equal([Claims.ResourceBased.Server.Operate], listedRole.Permissions);
    }

    [Fact]
    public async Task Grant_RejectsAnExistingPrefixClaimWithTheExactTuple()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("prefix-target");
        store.DbContext.Users.Add(target);
        store.DbContext.Claims.Add(Grant(
            Claims.ResourceBased.Server.View,
            seed.Server.PrimaryKey.ToString("D"),
            isPrefix: true));
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.GrantUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.View,
            AccessPrincipal(seed)));

        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    [Theory]
    [InlineData("sv:view:child")]
    [InlineData("SV:VIEW")]
    [InlineData("sv:view ")]
    [InlineData("adm:rls")]
    public async Task Grant_RejectsNearMatchOrNonServerPermissions(string permission)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("invalid-permission-target");
        store.DbContext.Users.Add(target);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        await Assert.ThrowsAsync<HostingValidationException>(() => service.GrantUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            permission,
            AccessPrincipal(seed)));

        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    [Fact]
    public async Task ModifyPermission_CannotListGrantOrRevokeAccess()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("modify-only-target");
        var role = new Role
        {
            Title = "Modify-only target role",
            Claims = [],
            Users = [],
        };
        store.DbContext.AddRange(target, role);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var modifyOnly = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Modify);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.GetAsync(
            seed.Server.PrimaryKey,
            modifyOnly));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.GrantUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.View,
            modifyOnly));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.RevokeUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.View,
            modifyOnly));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.GrantRoleAsync(
            seed.Server.PrimaryKey,
            role.PrimaryKey,
            Claims.ResourceBased.Server.View,
            modifyOnly));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.RevokeRoleAsync(
            seed.Server.PrimaryKey,
            role.PrimaryKey,
            Claims.ResourceBased.Server.View,
            modifyOnly));

        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
        store.DbContext.ChangeTracker.Clear();
        var persistedTarget = await store.DbContext.Users
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == target.PrimaryKey);
        Assert.Empty(persistedTarget.Claims!);
        var persistedRole = await store.DbContext.Roles
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == role.PrimaryKey);
        Assert.Empty(persistedRole.Claims!);
    }

    [Fact]
    public async Task ManageAccessAlone_CannotGrantFullControlToSelf()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);
        var manageAccessOnly = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.ManageAccess);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.GrantUserAsync(
            seed.Server.PrimaryKey,
            seed.User.PrimaryKey,
            Claims.ResourceBased.Server.All,
            manageAccessOnly));

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.Users
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == seed.User.PrimaryKey);
        Assert.DoesNotContain(persisted.Claims!, q => q.Identifier == Claims.ResourceBased.Server.All
                                                      && q.Value == seed.Server.PrimaryKey.ToString("D"));
        Assert.Empty(await store.DbContext.HostingAuditRecords
            .Where(q => q.Kind == EHostingAuditEventKind.AccessGranted)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ManageAccessWithView_CanOnlyAssignViewAndManageAccessToUsersAndRoles()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("scoped-target");
        var role = CreateRole("Scoped role");
        store.DbContext.AddRange(target, role);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var caller = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.ManageAccess,
            Claims.ResourceBased.Server.View);
        var serverId = seed.Server.PrimaryKey;

        await service.GrantUserAsync(serverId, target.PrimaryKey, Claims.ResourceBased.Server.View, caller);
        await service.GrantUserAsync(serverId, target.PrimaryKey, Claims.ResourceBased.Server.ManageAccess, caller);
        await service.GrantRoleAsync(serverId, role.PrimaryKey, Claims.ResourceBased.Server.View, caller);
        await service.RevokeRoleAsync(serverId, role.PrimaryKey, Claims.ResourceBased.Server.View, caller);
        await service.RevokeUserAsync(serverId, target.PrimaryKey, Claims.ResourceBased.Server.ManageAccess, caller);
        var auditBefore = await store.DbContext.HostingAuditRecords.CountAsync();

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.GrantUserAsync(
            serverId,
            target.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            caller));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.RevokeUserAsync(
            serverId,
            target.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            caller));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.GrantRoleAsync(
            serverId,
            role.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            caller));
        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.RevokeRoleAsync(
            serverId,
            role.PrimaryKey,
            Claims.ResourceBased.Server.Operate,
            caller));

        Assert.Equal(auditBefore, await store.DbContext.HostingAuditRecords.CountAsync());
        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.Users
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == target.PrimaryKey);
        Assert.Equal(
            [Claims.ResourceBased.Server.View],
            persisted.Claims!.Select(q => q.Identifier));
    }

    [Fact]
    public async Task FullControlHolder_CanGrantEveryAllowlistedPermission()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("full-control-target");
        store.DbContext.Users.Add(target);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var fullControl = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.All);

        foreach (var permission in service.AllowedPermissions)
        {
            await service.GrantUserAsync(seed.Server.PrimaryKey, target.PrimaryKey, permission, fullControl);
        }

        var listed = await service.GetAsync(seed.Server.PrimaryKey, fullControl);
        Assert.Equal(service.AllowedPermissions, Assert.Single(listed.Users).Permissions);
        Assert.Equal(service.AllowedPermissions, listed.AssignablePermissions);
    }

    [Theory]
    [InlineData(Claims.Administrative.All)]
    [InlineData(Claims.Administrative.Server)]
    [InlineData(Claims.Administrative.Hosting)]
    public async Task EmptyValuedAdministrativeClaim_CanGrantFullControlWithoutAServerClaim(string identifier)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var target = CreateUser("admin-target");
        store.DbContext.Users.Add(target);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);
        var administrator = HostingTestData.Principal(seed.User, (identifier, string.Empty));

        await service.GrantUserAsync(
            seed.Server.PrimaryKey,
            target.PrimaryKey,
            Claims.ResourceBased.Server.All,
            administrator);

        var listed = await service.GetAsync(seed.Server.PrimaryKey, administrator);
        Assert.Equal(service.AllowedPermissions, listed.AssignablePermissions);
        Assert.Equal([Claims.ResourceBased.Server.All], Assert.Single(listed.Users).Permissions);
    }

    [Fact]
    public async Task Get_ReportsAssignablePermissionsInAllowlistOrderForTheCaller()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);
        var caller = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.ManageAccess,
            Claims.ResourceBased.Server.View);

        var listed = await service.GetAsync(seed.Server.PrimaryKey, caller);

        Assert.Equal(
            [Claims.ResourceBased.Server.View, Claims.ResourceBased.Server.ManageAccess],
            listed.AssignablePermissions);
        Assert.Equal(service.AllowedPermissions, listed.AllowedPermissions);
    }

    [Fact]
    public async Task Get_IgnoresClaimsHeldForAnotherServerOrWithAnotherValueType()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);
        var identity = new System.Security.Claims.ClaimsIdentity(X39.UnitedTacticalForces.Api.Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new System.Security.Claims.Claim(
            System.Security.Claims.ClaimTypes.Name,
            seed.User.PrimaryKey.ToString()));
        identity.AddClaim(new System.Security.Claims.Claim(
            Claims.ResourceBased.Server.ManageAccess,
            seed.Server.PrimaryKey.ToString("D")));
        identity.AddClaim(new System.Security.Claims.Claim(
            Claims.ResourceBased.Server.Operate,
            Guid.NewGuid().ToString("D")));
        identity.AddClaim(new System.Security.Claims.Claim(
            Claims.ResourceBased.Server.Modify,
            seed.Server.PrimaryKey.ToString("D"),
            "custom-value-type"));
        var caller = new System.Security.Claims.ClaimsPrincipal(identity);

        var listed = await service.GetAsync(seed.Server.PrimaryKey, caller);

        Assert.Equal([Claims.ResourceBased.Server.ManageAccess], listed.AssignablePermissions);
    }

    [Fact]
    public async Task Create_KeepsCreatorExplicitFullScopedGrantAndAuditEvidence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        var server = await service.CreateAsync(
            new GameServerCreationPayload
            {
                Title = "Creator-owned server",
                GameIdentifier = HostingTestData.GameIdentifier,
                RuntimeProviderIdentifier = TestRuntimeProvider.Identifier,
            },
            HostingTestData.Principal(seed.User));

        store.DbContext.ChangeTracker.Clear();
        var creator = await store.DbContext.Users
            .Include(q => q.Claims)
            .SingleAsync(q => q.PrimaryKey == seed.User.PrimaryKey);
        var grant = Assert.Single(creator.Claims!, q =>
            q.Identifier == Claims.ResourceBased.Server.All
            && q.Value == server.PrimaryKey.ToString("D"));
        Assert.Null(grant.ValueType);
        Assert.False(grant.IsPrefix);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.AccessGranted
                 && q.ActorFk == seed.User.PrimaryKey
                 && q.GameServerFk == server.PrimaryKey);
    }

    [Fact]
    public async Task SearchUserCandidates_ReturnsOnlyActiveMembersWithoutExactGrantOrderedByNicknameThenId()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var serverValue = seed.Server.PrimaryKey.ToString("D");
        var low = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var alphaLow = CreateVerifiedUser("alpha", low);
        var alphaHigh = CreateVerifiedUser("alpha", high);
        var bravo = CreateVerifiedUser("bravo");
        var granted = CreateVerifiedUser("charlie-granted");
        granted.Claims = [Grant(Claims.ResourceBased.Server.View, serverValue)];
        var otherServer = CreateVerifiedUser("delta-other-server");
        otherServer.Claims = [Grant(Claims.ResourceBased.Server.DeleteData, Guid.NewGuid().ToString("D"))];
        var prefixGrant = CreateVerifiedUser("echo-prefix");
        prefixGrant.Claims = [Grant(Claims.ResourceBased.Server.ManageAccess, serverValue, isPrefix: true)];
        var typedGrant = CreateVerifiedUser("foxtrot-typed");
        typedGrant.Claims = [Grant(Claims.ResourceBased.Server.Modify, serverValue, valueType: "custom")];
        var childGrant = CreateVerifiedUser("golf-child");
        childGrant.Claims = [Grant(Claims.ResourceBased.Server.Operate + ":child", serverValue)];
        var deleted = CreateVerifiedUser("hotel-deleted");
        deleted.IsDeleted = true;
        var banned = CreateVerifiedUser("india-banned");
        banned.IsBanned = true;
        var unverified = CreateVerifiedUser("juliet-unverified");
        unverified.IsVerified = false;
        store.DbContext.Users.AddRange(
            alphaHigh,
            bravo,
            granted,
            otherServer,
            prefixGrant,
            typedGrant,
            childGrant,
            deleted,
            banned,
            unverified,
            alphaLow);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var result = await service.SearchUserCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 50,
            AccessPrincipal(seed));

        var candidates = result
            .Where(q => q.UserId != seed.User.PrimaryKey)
            .Select(q => (q.Nickname, q.UserId))
            .ToArray();
        Assert.Equal(
            [
                ("alpha", low),
                ("alpha", high),
                ("bravo", bravo.PrimaryKey),
                ("delta-other-server", otherServer.PrimaryKey),
                ("echo-prefix", prefixGrant.PrimaryKey),
                ("foxtrot-typed", typedGrant.PrimaryKey),
                ("golf-child", childGrant.PrimaryKey),
            ],
            candidates);
    }

    [Fact]
    public async Task SearchUserCandidates_HonorsTakeAndProjectsTheFingerprintWithoutTheChatProviderName()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var userId = Guid.Parse("11111111-2222-3333-4444-5555aabbccdd");
        var user = CreateVerifiedUser("alpha", userId);
        user.Discord = new DiscordUser { Username = "secret-chat-name" };
        store.DbContext.Users.AddRange(user, CreateVerifiedUser("bravo"), CreateVerifiedUser("charlie"));
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var result = await service.SearchUserCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 2,
            AccessPrincipal(seed));

        Assert.Equal(2, result.Length);
        var candidate = Assert.Single(result, q => q.UserId == userId);
        Assert.Equal("#bbccdd", candidate.Detail);
        Assert.All(result, q => Assert.DoesNotContain("secret-chat-name", q.Nickname + q.Detail));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task SearchUserCandidates_RejectsATakeOutsideOneToFifty(int take)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);

        await Assert.ThrowsAsync<HostingValidationException>(() => service.SearchUserCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take,
            AccessPrincipal(seed)));
    }

    [Fact]
    public async Task SearchUserCandidates_RequiresManageAccessAndAnExistingServer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);
        var modifyOnly = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Modify);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.SearchUserCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 20,
            modifyOnly));
        await Assert.ThrowsAsync<HostingNotFoundException>(() => service.SearchUserCandidatesAsync(
            Guid.NewGuid(),
            search: null,
            take: 20,
            HostingTestData.Principal(seed.User, (Claims.Administrative.Hosting, string.Empty))));
    }

    [Fact]
    public async Task SearchRoleCandidates_ExcludesExactGrantsOrdersByTitleThenIdAndHonorsTake()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var serverValue = seed.Server.PrimaryKey.ToString("D");
        var alphaFirst = CreateRole("Alpha");
        var alphaSecond = CreateRole("Alpha");
        var bravo = CreateRole("Bravo");
        var granted = CreateRole("Charlie granted");
        granted.Claims = [Grant(Claims.ResourceBased.Server.View, serverValue)];
        var otherServer = CreateRole("Delta other server");
        otherServer.Claims = [Grant(Claims.ResourceBased.Server.DeleteData, Guid.NewGuid().ToString("D"))];
        var prefixGrant = CreateRole("Echo prefix");
        prefixGrant.Claims = [Grant(Claims.ResourceBased.Server.ManageAccess, serverValue, isPrefix: true)];
        store.DbContext.Roles.Add(alphaFirst);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.Roles.Add(alphaSecond);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.Roles.AddRange(bravo, granted, otherServer, prefixGrant);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var all = await service.SearchRoleCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 50,
            AccessPrincipal(seed));
        var limited = await service.SearchRoleCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 2,
            AccessPrincipal(seed));

        Assert.Equal(
            [alphaFirst.PrimaryKey, alphaSecond.PrimaryKey, bravo.PrimaryKey, otherServer.PrimaryKey, prefixGrant.PrimaryKey],
            all.Select(q => q.RoleId));
        Assert.Equal([alphaFirst.PrimaryKey, alphaSecond.PrimaryKey], limited.Select(q => q.RoleId));
        Assert.True(alphaFirst.PrimaryKey < alphaSecond.PrimaryKey);
    }

    [Fact]
    public async Task SearchRoleCandidates_CountsOnlyActiveMembers()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var active = CreateVerifiedUser("active-one");
        var activeToo = CreateVerifiedUser("active-two");
        var deleted = CreateVerifiedUser("deleted-member");
        deleted.IsDeleted = true;
        var banned = CreateVerifiedUser("banned-member");
        banned.IsBanned = true;
        var unverified = CreateVerifiedUser("unverified-member");
        unverified.IsVerified = false;
        var counted = CreateRole("Counted");
        counted.Users = [active, activeToo, deleted, banned, unverified];
        var empty = CreateRole("Empty");
        store.DbContext.Roles.AddRange(counted, empty);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var result = await service.SearchRoleCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 20,
            AccessPrincipal(seed));

        Assert.Equal(2, Assert.Single(result, q => q.RoleId == counted.PrimaryKey).MemberCount);
        Assert.Equal(0, Assert.Single(result, q => q.RoleId == empty.PrimaryKey).MemberCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task SearchRoleCandidates_RejectsATakeOutsideOneToFifty(int take)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);

        await Assert.ThrowsAsync<HostingValidationException>(() => service.SearchRoleCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take,
            AccessPrincipal(seed)));
    }

    [Fact]
    public async Task SearchRoleCandidates_RequiresManageAccessAndAnExistingServer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = CreateService(store);
        var modifyOnly = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Modify);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.SearchRoleCandidatesAsync(
            seed.Server.PrimaryKey,
            search: null,
            take: 20,
            modifyOnly));
        await Assert.ThrowsAsync<HostingNotFoundException>(() => service.SearchRoleCandidatesAsync(
            Guid.NewGuid(),
            search: null,
            take: 20,
            HostingTestData.Principal(seed.User, (Claims.Administrative.Hosting, string.Empty))));
    }

    [Fact]
    public async Task List_ProjectsTheUserFingerprintAndTheRoleActiveMemberCount()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var serverValue = seed.Server.PrimaryKey.ToString("D");
        var userId = Guid.Parse("11111111-2222-3333-4444-5555aabbccdd");
        var user = CreateVerifiedUser("granted-user", userId);
        user.Discord = new DiscordUser { Username = "secret-chat-name" };
        user.Claims = [Grant(Claims.ResourceBased.Server.View, serverValue)];
        var member = CreateVerifiedUser("role-member");
        var banned = CreateVerifiedUser("banned-role-member");
        banned.IsBanned = true;
        var role = CreateRole("Granted role");
        role.Users = [member, banned];
        role.Claims = [Grant(Claims.ResourceBased.Server.Operate, serverValue)];
        store.DbContext.AddRange(user, role);
        await store.DbContext.SaveChangesAsync();
        var service = CreateService(store);

        var result = await service.GetAsync(seed.Server.PrimaryKey, AccessPrincipal(seed));

        var listedUser = Assert.Single(result.Users);
        Assert.Equal("#bbccdd", listedUser.Detail);
        Assert.DoesNotContain("secret-chat-name", listedUser.Nickname + listedUser.Detail);
        var listedRole = Assert.Single(result.Roles);
        Assert.Equal(1, listedRole.MemberCount);
    }

    /// <summary>
    /// The role title search translates for Npgsql to a case-insensitive ILIKE with the backslash escape clause and
    /// the escaped pattern, proven offline with <c>ToQueryString</c>.
    /// </summary>
    [Fact]
    public void ApplyTitleSearch_TranslatesForNpgsqlToEscapedIlike()
    {
        using var dbContext = CreateNpgsqlModelDbContext();

        var sql = GameServerAccessService
            .ApplyTitleSearch(dbContext.Roles.AsNoTracking(), " 50%_a\\b ")
            .ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.Ordinal);
        Assert.Contains("ESCAPE", sql, StringComparison.Ordinal);
        Assert.Contains("%50\\%\\_a\\\\b%", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// The role candidate query the service itself runs translates for Npgsql with the escaped title search, the
    /// exclusion of roles that already hold a permission on the server, the ordering, the limit and the active member
    /// count, proven offline with <c>ToQueryString</c>. The query is built by the production method, so a change to
    /// its predicates changes this SQL.
    /// </summary>
    [Fact]
    public void BuildRoleCandidateQuery_TranslatesForNpgsqlWithEveryFilter()
    {
        using var dbContext = CreateNpgsqlModelDbContext();

        var sql = GameServerAccessService
            .BuildRoleCandidateQuery(dbContext.Roles.AsNoTracking(), "server-value-1", " 50%_a ", 7)
            .ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.Ordinal);
        Assert.Contains("ESCAPE", sql, StringComparison.Ordinal);
        Assert.Contains("%50\\%\\_a%", sql, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("server-value-1", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsBanned\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsDeleted\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsVerified\"", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains(Claims.ResourceBased.Server.ManageAccess, sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every character that is special in a LIKE pattern is escaped exactly once, in one pass, so each of them is
    /// matched literally whatever the combination in the search text.
    /// </summary>
    [Fact]
    public void LikePatternEscape_EscapesEveryWildcardCharacterOnce()
    {
        Assert.Equal("a\\\\b\\%c\\_d\\[e\\]f\\^g", LikePattern.Escape("  a\\b%c_d[e]f^g "));
        Assert.Equal("%x\\%y%", LikePattern.Contains("x%y"));
        Assert.Throws<ArgumentNullException>(() => LikePattern.Escape(null!));
    }

    /// <summary>
    /// The nickname search translates for Npgsql to a case-insensitive ILIKE that carries the backslash escape
    /// clause and the escaped pattern, proven offline with <c>ToQueryString</c> on a metadata-only context.
    /// </summary>
    [Fact]
    public void ApplyNicknameSearch_TranslatesForNpgsqlToEscapedIlike()
    {
        using var dbContext = CreateNpgsqlModelDbContext();

        var sql = GameServerAccessService
            .ApplyNicknameSearch(dbContext.Users.AsNoTracking(), " 50%_a\\b ")
            .ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.Ordinal);
        Assert.Contains("ESCAPE", sql, StringComparison.Ordinal);
        Assert.Contains("%50\\%\\_a\\\\b%", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyNicknameSearch_WithBlankSearch_AddsNoFilter(string? search)
    {
        using var dbContext = CreateNpgsqlModelDbContext();
        var query = dbContext.Users.AsNoTracking();

        var sql = GameServerAccessService.ApplyNicknameSearch(query, search).ToQueryString();

        Assert.DoesNotContain("ILIKE", sql, StringComparison.Ordinal);
    }

    private static DatabaseContext CreateNpgsqlModelDbContext()
    {
        var options = new DbContextOptionsBuilder<NpgsqlModelDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=metadata-only;Username=metadata;Password=metadata",
                npgsqlOptions => npgsqlOptions.UseNodaTime())
            .Options;
        return new NpgsqlModelDbContext(options);
    }

    private static Role CreateRole(string title)
        => new()
        {
            Title = title,
            Description = title + " description",
            Claims = [],
            Users = [],
        };

    private static User CreateVerifiedUser(string nickname, Guid? id = null)
    {
        var user = CreateUser(nickname);
        user.IsVerified = true;
        if (id is { } value)
            user.PrimaryKey = value;
        return user;
    }

    private sealed class NpgsqlModelDbContext(DbContextOptions<NpgsqlModelDbContext> dbContextOptions)
        : DatabaseContext(dbContextOptions);

    private static GameServerAccessService CreateService(HostingTestStore store)
        => new(store.DbContext, new HostingAuditWriter());

    /// <summary>
    /// Builds a caller that may manage access and holds every non-full server permission, so that it may assign
    /// each of them but not full control.
    /// </summary>
    private static System.Security.Claims.ClaimsPrincipal AccessPrincipal(
        HostingTestSeed seed)
        => HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.ManageAccess,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Operate,
            Claims.ResourceBased.Server.Modify,
            Claims.ResourceBased.Server.DeleteData,
            Claims.ResourceBased.Server.ManageSecrets,
            Claims.ResourceBased.Server.RevealSecrets);

    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = nickname,
            AvatarMimeType = string.Empty,
            Claims = [],
            Roles = [],
            Discord = new DiscordUser { Username = nickname },
            Steam = new SteamUser(),
        };

    private static Claim Grant(
        string identifier,
        string value,
        string? valueType = null,
        bool isPrefix = false)
        => new()
        {
            Identifier = identifier,
            Value = value,
            ValueType = valueType,
            IsPrefix = isPrefix,
            Category = "test",
            Title = identifier,
            Description = "test grant",
        };
}
