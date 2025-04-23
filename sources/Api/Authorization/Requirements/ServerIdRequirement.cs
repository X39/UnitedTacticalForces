using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// The base class for server id requirements.
/// </summary>
[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public abstract class ServerIdRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// The name of the route parameter to check against.
    /// </summary>
    public const string RouteParameterName = "gameServerId";

    /// <inheritdoc />
    public override string PolicyName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ServerIdRequirement"/>.
    /// </summary>
    /// <param name="claim">The claim required to have the value of the route parameter.</param>
    protected ServerIdRequirement(string claim)
        : base(
        [
            (Claims.Administrative.All, null),
                (Claims.Administrative.Server, null),
                (Claims.ResourceBased.Server.All, RouteParameterName),
                (claim, RouteParameterName),
        ]
        )
    {
        PolicyName = claim;
    }

    /// <summary>
    /// Creates a new instance of <see cref="ServerIdRequirement"/>.
    /// </summary>
    /// <param name="policyName">The name of the policy to register this requirement to.</param>
    /// <param name="claims">The claims required to have the value of the route parameter.</param>
    protected ServerIdRequirement(string policyName, IEnumerable<string> claims)
        : base(
            new[]
            {
                (Claims.Administrative.All, null),
                (Claims.Administrative.Server, null),
                (Claims.ResourceBased.Server.All, RouteParameterName),
            }.Concat(claims.Select(claim => (claim, RouteParameterName))!))
    {
        PolicyName = policyName;
    }
}