using System.Security.Claims;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for granting or revoking access to one server.
/// </summary>
public sealed class ServerManageAccessRequirement : ServerIdRequirement
{
    public ServerManageAccessRequirement() : base(Claims.ResourceBased.Server.ManageAccess)
    {
    }

    /// <inheritdoc />
    public override ValueTask<bool> IsSatisfiedAsync(HttpContext httpContext, IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
        if (!httpContext.Request.RouteValues.TryGetValue(RouteParameterName, out var routeValue))
            return ValueTask.FromResult(false);
        if (!Guid.TryParse(routeValue?.ToString(), out var gameServerId))
            return ValueTask.FromResult(false);
        var serverValue = gameServerId.ToString("D");
        return ValueTask.FromResult(HasExactPermission(httpContext.User, serverValue));
    }

    internal static bool HasExactPermission(ClaimsPrincipal principal, string serverValue)
        => principal.Claims.Any(claim =>
            string.Equals(claim.ValueType, ClaimValueTypes.String, StringComparison.Ordinal)
            && ((IsExact(claim, Claims.Administrative.All, string.Empty)
                 || IsExact(claim, Claims.Administrative.Server, string.Empty)
                 || IsExact(claim, Claims.Administrative.Hosting, string.Empty))
                || IsExact(claim, Claims.ResourceBased.Server.All, serverValue)
                || IsExact(claim, Claims.ResourceBased.Server.ManageAccess, serverValue)));

    private static bool IsExact(Claim claim, string type, string value)
        => string.Equals(claim.Type, type, StringComparison.Ordinal)
           && string.Equals(claim.Value, value, StringComparison.Ordinal);
}
