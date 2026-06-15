using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Authorization.Requirements;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using SecurityClaim = System.Security.Claims.Claim;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerAccessControllerAuthorizationTests
{
    [Fact]
    public void GetServerRoute_HasExplicitNameForCreatedResponseLocation()
    {
        var method = typeof(GameServersController).GetMethod(nameof(GameServersController.GetAsync));
        var route = method?.GetCustomAttribute<HttpGetAttribute>();

        Assert.Equal(
            nameof(GameServersController) + "." + nameof(GameServersController.GetAsync),
            route?.Name);
    }

    [Fact]
    public void DeleteServerRoute_RequiresScopedModifyPermission()
    {
        var method = typeof(GameServersController).GetMethod(nameof(GameServersController.DeleteAsync))
                     ?? throw new InvalidOperationException("Delete endpoint was not found.");
        var route = Assert.Single(method.GetCustomAttributes<HttpDeleteAttribute>(inherit: true));
        var policy = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal("{gameServerId:guid}", route.Template);
        Assert.Equal(Claims.ResourceBased.Server.Modify, policy.Policy);
    }

    [Fact]
    public async Task ManageAccessRequirement_AcceptsOnlyExactTypeValueAndStringValueType()
    {
        var serverId = Guid.NewGuid();

        Assert.True(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(Claims.ResourceBased.Server.ManageAccess, serverId.ToString("D"))));
        Assert.True(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(Claims.ResourceBased.Server.All, serverId.ToString("D"))));
        Assert.True(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(Claims.Administrative.Hosting, string.Empty)));
        Assert.False(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(Claims.ResourceBased.Server.Modify, serverId.ToString("D"))));
        Assert.False(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(Claims.Administrative.Role, string.Empty)));
        Assert.False(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(Claims.ResourceBased.Server.ManageAccess, Guid.NewGuid().ToString("D"))));
        Assert.False(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(
                Claims.ResourceBased.Server.ManageAccess + ":child",
                serverId.ToString("D"))));
        Assert.False(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(
                Claims.ResourceBased.Server.ManageAccess,
                serverId.ToString("D") + "-child")));
        Assert.False(await IsAuthorizedAsync(
            serverId,
            new SecurityClaim(
                Claims.ResourceBased.Server.ManageAccess,
                serverId.ToString("D"),
                "custom-value-type")));
    }

    [Fact]
    public void AccessEndpoints_RequireServerManageAccessWithoutGlobalRoleAdministration()
    {
        foreach (var methodName in new[]
                 {
                     nameof(GameServersController.GetAccessAsync),
                     nameof(GameServersController.GrantUserAccessAsync),
                     nameof(GameServersController.RevokeUserAccessAsync),
                     nameof(GameServersController.GrantRoleAccessAsync),
                     nameof(GameServersController.RevokeRoleAccessAsync),
                 })
        {
            var method = typeof(GameServersController).GetMethod(methodName)
                         ?? throw new InvalidOperationException($"Method {methodName} was not found.");
            var policy = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
            Assert.Equal(Claims.ResourceBased.Server.ManageAccess, policy.Policy);
            Assert.NotEqual(Claims.Administrative.Role, policy.Policy);
        }

        var genericControllerPolicy = Assert.Single(
            typeof(ResourceClaimController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal(Claims.Administrative.Role, genericControllerPolicy.Policy);
    }

    [Fact]
    public async Task RoleGrantEndpoint_DelegatesServerRolePermissionWithAuthenticatedActor()
    {
        var actorId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var accessService = new RecordingAccessService();
        var controller = CreateController(accessService, actorId);

        var result = await controller.GrantRoleAccessAsync(
            serverId,
            new GameServerRoleAccessPayload
            {
                RoleId = 42,
                Permission = Claims.ResourceBased.Server.Operate,
            },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(
            (serverId, 42L, Claims.ResourceBased.Server.Operate, actorId),
            accessService.LastRoleGrant);
    }

    [Fact]
    public async Task GetAccessEndpoint_ReturnsScopedGrantProjection()
    {
        var serverId = Guid.NewGuid();
        var expected = new GameServerAccessDto
        {
            AllowedPermissions = [Claims.ResourceBased.Server.View],
        };
        var accessService = new RecordingAccessService { Access = expected };
        var controller = CreateController(accessService, Guid.NewGuid());

        var result = await controller.GetAccessAsync(serverId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Equal(serverId, accessService.LastListedServerId);
    }

    [Fact]
    public async Task MutationEndpoint_WithoutActorIdentity_ReturnsUnauthorizedWithoutDelegating()
    {
        var accessService = new RecordingAccessService();
        var controller = CreateController(accessService, actorId: null);

        var result = await controller.GrantUserAccessAsync(
            Guid.NewGuid(),
            new GameServerAccessPayload
            {
                UserId = Guid.NewGuid(),
                Permission = Claims.ResourceBased.Server.View,
            },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(accessService.LastUserGrant);
    }

    private static async Task<bool> IsAuthorizedAsync(Guid serverId, SecurityClaim claim)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([claim], Constants.AuthorizationSchemas.Api)),
        };
        context.Request.RouteValues[ServerIdRequirement.RouteParameterName] = serverId;
        await using var provider = new ServiceCollection().BuildServiceProvider();
        return await new ServerManageAccessRequirement().IsSatisfiedAsync(context, provider);
    }

    private static GameServersController CreateController(
        IGameServerAccessService accessService,
        Guid? actorId)
    {
        var controller = new GameServersController(null!, null!, accessService);
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        if (actorId is { } value)
            identity.AddClaim(new SecurityClaim(ClaimTypes.Name, value.ToString("D")));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
        return controller;
    }

    private sealed class RecordingAccessService : IGameServerAccessService
    {
        public IReadOnlyCollection<string> AllowedPermissions => Access.AllowedPermissions;
        public GameServerAccessDto Access { get; init; } = new();
        public Guid? LastListedServerId { get; private set; }
        public (Guid serverId, Guid userId, string permission, Guid actorId)? LastUserGrant { get; private set; }
        public (Guid serverId, long roleId, string permission, Guid actorId)? LastRoleGrant { get; private set; }

        public Task<GameServerAccessDto> GetAsync(
            Guid gameServerId,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            LastListedServerId = gameServerId;
            return Task.FromResult(Access);
        }

        public Task GrantUserAsync(
            Guid gameServerId,
            Guid userId,
            string permission,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            LastUserGrant = (gameServerId, userId, permission, ActorId(principal));
            return Task.CompletedTask;
        }

        public Task RevokeUserAsync(
            Guid gameServerId,
            Guid userId,
            string permission,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task GrantRoleAsync(
            Guid gameServerId,
            long roleId,
            string permission,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            LastRoleGrant = (gameServerId, roleId, permission, ActorId(principal));
            return Task.CompletedTask;
        }

        public Task RevokeRoleAsync(
            Guid gameServerId,
            long roleId,
            string permission,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private static Guid ActorId(ClaimsPrincipal principal)
            => Guid.Parse(principal.FindFirst(ClaimTypes.Name)!.Value);
    }
}
