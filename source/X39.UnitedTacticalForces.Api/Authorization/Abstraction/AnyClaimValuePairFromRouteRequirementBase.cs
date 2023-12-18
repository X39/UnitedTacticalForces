using System.Collections.Immutable;

namespace X39.UnitedTacticalForces.Api.Authorization.Abstraction;

/// <summary>
/// Provides a base class for app requirements.
/// </summary>
public abstract class AnyClaimValuePairFromRouteRequirementBase : IAppRequirement
{
    private readonly IReadOnlyCollection<(string claim, string? routeParameter)> _claims;

    /// <summary>
    /// Creates a new instance of <see cref="AnyClaimValuePairFromRouteRequirementBase"/>.
    /// </summary>
    /// <param name="claims">The claims required to satisfy this requirement.</param>
    protected AnyClaimValuePairFromRouteRequirementBase(IEnumerable<(string claim, string? routeParameter)> claims)
    {
        _claims = claims.ToImmutableArray();
    }

    /// <inheritdoc />
    public abstract string PolicyName { get; }

    /// <inheritdoc />
    public virtual ValueTask<bool> IsSatisfiedAsync(HttpContext httpContext, IServiceProvider serviceProvider)
    {
        foreach (var (claim, routeParameter) in _claims)
        {
            if (routeParameter is null)
            {
                if (httpContext.User.HasClaim(claim, string.Empty))
                    return ValueTask.FromResult(true);
            }
            else if (httpContext.Request.RouteValues.TryGetValue(routeParameter, out var routeValue))
            {
                if (httpContext.User.HasClaim(claim, routeValue?.ToString() ?? string.Empty))
                    return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }
}