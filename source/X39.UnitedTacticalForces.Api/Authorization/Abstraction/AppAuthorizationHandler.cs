using Microsoft.AspNetCore.Authorization;

namespace X39.UnitedTacticalForces.Api.Authorization.Abstraction;

/// <summary>
/// Custom authorization handler for the app authorization system.
/// </summary>
public sealed class AppAuthorizationHandler : IAuthorizationHandler
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new instance of <see cref="AppAuthorizationHandler"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public AppAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    /// <inheritdoc />
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext httpContext)
            throw new NullReferenceException("The AuthorizationHandlerContext.Resource is not a http context.");
        foreach (var appRequirement in context.Requirements.OfType<IAppRequirement>())
        {
            if (await appRequirement.IsSatisfiedAsync(httpContext, _serviceProvider))
                context.Succeed(appRequirement);
        }
    }
}