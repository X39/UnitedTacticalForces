using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
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
            ],
            service.AllowedPermissions);
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

    private static GameServerAccessService CreateService(HostingTestStore store)
        => new(store.DbContext, new HostingAuditWriter());

    private static System.Security.Claims.ClaimsPrincipal AccessPrincipal(
        HostingTestSeed seed)
        => HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.ManageAccess);

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
