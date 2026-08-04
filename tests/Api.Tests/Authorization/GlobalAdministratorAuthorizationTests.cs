using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace Api.Tests.Authorization;

public sealed class GlobalAdministratorAuthorizationTests
{
    [Fact]
    public async Task AdministrativeAll_SatisfiesEveryRegisteredApplicationRequirement()
    {
        var principal = CreateGlobalAdministrator();
        var httpContext = new DefaultHttpContext { User = principal };
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var requirementTypes = typeof(X39.UnitedTacticalForces.Api.Program).Assembly
            .GetTypes()
            .Where(q => !q.IsAbstract && q.IsAssignableTo(typeof(IAppRequirement)))
            .OrderBy(q => q.FullName)
            .ToArray();

        Assert.NotEmpty(requirementTypes);
        foreach (var requirementType in requirementTypes)
        {
            var requirement = Assert.IsAssignableFrom<IAppRequirement>(Activator.CreateInstance(requirementType));
            Assert.True(
                await requirement.IsSatisfiedAsync(httpContext, serviceProvider),
                $"{requirementType.FullName} rejected the global administrator claim.");
        }
    }

    [Fact]
    public async Task AuthorizationHandler_SucceedsApplicationPoliciesForAdministrativeAll()
    {
        var principal = CreateGlobalAdministrator();
        var httpContext = new DefaultHttpContext { User = principal };
        var requirement = new AlwaysDeniedRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, httpContext);
        var handler = new AppAuthorizationHandler(new ServiceCollection().BuildServiceProvider());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static ClaimsPrincipal CreateGlobalAdministrator()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.Name, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(Claims.Administrative.All, string.Empty));
        return new ClaimsPrincipal(identity);
    }

    private sealed class AlwaysDeniedRequirement : IAppRequirement
    {
        public string PolicyName => "test:always-denied";

        public ValueTask<bool> IsSatisfiedAsync(HttpContext httpContext, IServiceProvider serviceProvider)
            => ValueTask.FromResult(false);
    }
}
