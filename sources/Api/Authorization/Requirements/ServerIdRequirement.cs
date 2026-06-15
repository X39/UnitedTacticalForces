using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Base requirement for authorization scoped to a game-server route identifier.
/// </summary>
[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public abstract class ServerIdRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    public const string RouteParameterName = "gameServerId";

    protected ServerIdRequirement(string policyName)
        : base(
        [
            (Claims.Administrative.All, null),
            (Claims.Administrative.Server, null),
            (Claims.Administrative.Hosting, null),
            (Claims.ResourceBased.Server.All, RouteParameterName),
            (policyName, RouteParameterName),
        ])
    {
        PolicyName = policyName;
    }

    protected ServerIdRequirement(string policyName, IEnumerable<string> claims)
        : base(
            new (string claim, string? routeParameter)[]
            {
                (Claims.Administrative.All, null),
                (Claims.Administrative.Server, null),
                (Claims.Administrative.Hosting, null),
                (Claims.ResourceBased.Server.All, RouteParameterName),
            }.Concat(claims.Select(claim => (claim, routeParameter: (string?) RouteParameterName))))
    {
        PolicyName = policyName;
    }

    public override string PolicyName { get; }
}
