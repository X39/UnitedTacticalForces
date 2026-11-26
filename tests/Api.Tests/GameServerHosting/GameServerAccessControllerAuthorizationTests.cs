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

    [Theory]
    [InlineData(nameof(GameServersController.EstimateStorageDemandAsync), Claims.Creation.Server)]
    [InlineData(nameof(GameServersController.GetCreationModPackOptionsAsync), Claims.Creation.Server)]
    [InlineData(nameof(GameServersController.GetCreationCostOwnerOptionsAsync), Claims.Creation.Server)]
    [InlineData(nameof(GameServersController.GetCostOwnerOptionsAsync), Claims.ResourceBased.Server.Modify)]
    [InlineData(nameof(GameServersController.GetStorageDemandAsync), Claims.ResourceBased.Server.View)]
    [InlineData(nameof(GameServersController.ChangeRuntimeStorageAsync), Claims.ResourceBased.Server.Modify)]
    public void StorageEstimateRoutes_RequireExpectedAuthorization(string methodName, string expectedPolicy)
    {
        var method = typeof(GameServersController).GetMethod(methodName)
                     ?? throw new InvalidOperationException($"{methodName} endpoint was not found.");
        var policy = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal(expectedPolicy, policy.Policy);
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
                     nameof(GameServersController.GetUserAccessOptionsAsync),
                     nameof(GameServersController.GetRoleAccessOptionsAsync),
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

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-1)]
    public async Task UserOptionsEndpoint_WithTakeOutsideBounds_ReturnsBadRequestWithoutDelegating(int take)
    {
        var accessService = new RecordingAccessService();
        var controller = CreateController(accessService, Guid.NewGuid());

        var result = await controller.GetUserAccessOptionsAsync(
            Guid.NewGuid(),
            "alpha",
            take,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(accessService.LastUserSearch);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task UserOptionsEndpoint_WithTakeInsideBounds_DelegatesTheRequestedTake(int take)
    {
        var serverId = Guid.NewGuid();
        var expected = new[]
        {
            new GameServerUserAccessCandidateDto { UserId = Guid.NewGuid(), Nickname = "alpha", Detail = "#abcdef" },
        };
        var accessService = new RecordingAccessService { UserCandidates = expected };
        var controller = CreateController(accessService, Guid.NewGuid());

        var result = await controller.GetUserAccessOptionsAsync(serverId, "alpha", take, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Equal((serverId, "alpha", take), accessService.LastUserSearch);
    }

    [Fact]
    public async Task UserOptionsEndpoint_WithoutTake_UsesTheDefaultOfTwenty()
    {
        var serverId = Guid.NewGuid();
        var accessService = new RecordingAccessService();
        var controller = CreateController(accessService, Guid.NewGuid());

        var result = await controller.GetUserAccessOptionsAsync(serverId, null);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal((serverId, (string?)null, 20), accessService.LastUserSearch);
    }

    [Fact]
    public async Task UserOptionsEndpoint_WhenTheServiceForbids_ReturnsForbid()
    {
        var controller = CreateController(new RecordingAccessService { ForbidSearch = true }, Guid.NewGuid());

        var result = await controller.GetUserAccessOptionsAsync(Guid.NewGuid(), null, 20);

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task RoleOptionsEndpoint_WithTakeOutsideBounds_ReturnsBadRequestWithoutDelegating(int take)
    {
        var accessService = new RecordingAccessService();
        var controller = CreateController(accessService, Guid.NewGuid());

        var result = await controller.GetRoleAccessOptionsAsync(
            Guid.NewGuid(),
            "ops",
            take,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(accessService.LastRoleSearch);
    }

    [Fact]
    public async Task RoleOptionsEndpoint_DelegatesWithTheDefaultTakeOfTwenty()
    {
        var serverId = Guid.NewGuid();
        var expected = new[]
        {
            new GameServerRoleAccessCandidateDto { RoleId = 7, Title = "Operators", MemberCount = 3 },
        };
        var accessService = new RecordingAccessService { RoleCandidates = expected };
        var controller = CreateController(accessService, Guid.NewGuid());

        var result = await controller.GetRoleAccessOptionsAsync(serverId, "op");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Equal((serverId, "op", 20), accessService.LastRoleSearch);
    }

    /// <summary>The cost owner options route is a GET on the cost-owner/options template of one server.</summary>
    [Fact]
    public void CostOwnerOptionsRoute_UsesTheExpectedTemplate()
    {
        var method = typeof(GameServersController).GetMethod(nameof(GameServersController.GetCostOwnerOptionsAsync))
                     ?? throw new InvalidOperationException("Cost owner options endpoint was not found.");
        var route = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>(inherit: true));

        Assert.Equal("{gameServerId:guid}/cost-owner/options", route.Template);
    }

    /// <summary>A take outside 1 to 50 answers 400 on the cost owner options route without any lookup.</summary>
    /// <param name="take">The invalid limit.</param>
    /// <returns>A task completing after the bad request is verified.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-1)]
    public async Task CostOwnerOptionsEndpoint_WithTakeOutsideBounds_ReturnsBadRequestWithoutDelegating(int take)
    {
        var controller = new GameServersController(null!, null!, new RecordingAccessService());

        var result = await controller.GetCostOwnerOptionsAsync(Guid.NewGuid(), "alpha", take, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
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
        public bool ForbidSearch { get; init; }
        public GameServerRoleAccessCandidateDto[] RoleCandidates { get; init; } = [];
        public (Guid serverId, string? search, int take)? LastRoleSearch { get; private set; }
        public GameServerUserAccessCandidateDto[] UserCandidates { get; init; } = [];
        public (Guid serverId, string? search, int take)? LastUserSearch { get; private set; }
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

        public Task<GameServerUserAccessCandidateDto[]> SearchUserCandidatesAsync(
            Guid gameServerId,
            string? search,
            int take,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            if (ForbidSearch)
                throw new HostingForbiddenException("Server ManageAccess permission is required.");
            LastUserSearch = (gameServerId, search, take);
            return Task.FromResult(UserCandidates);
        }

        public Task<GameServerRoleAccessCandidateDto[]> SearchRoleCandidatesAsync(
            Guid gameServerId,
            string? search,
            int take,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            LastRoleSearch = (gameServerId, search, take);
            return Task.FromResult(RoleCandidates);
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
