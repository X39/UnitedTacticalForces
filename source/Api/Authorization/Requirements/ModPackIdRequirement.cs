using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// The base class for modPack id requirements.
/// </summary>
[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public abstract class ModPackIdRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// The name of the route parameter to check against.
    /// </summary>
    public const string RouteParameterName = "modPackId";

    /// <inheritdoc />
    public override string PolicyName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ModPackIdRequirement"/>.
    /// </summary>
    /// <param name="policyName">The claim required to have the value of the route parameter.</param>
    protected ModPackIdRequirement(string policyName)
        : base(
            new[]
            {
                (Claims.Administrative.All, null),
                (Claims.Administrative.ModPack, null),
                (Claims.ResourceBased.ModPack.All, RouteParameterName),
                (claim: policyName, RouteParameterName),
            })
    {
        PolicyName = policyName;
    }

    /// <summary>
    /// Creates a new instance of <see cref="ModPackIdRequirement"/>.
    /// </summary>
    /// <param name="policyName">The name of the policy to register this requirement to.</param>
    /// <param name="claims">The claims required to have the value of the route parameter.</param>
    protected ModPackIdRequirement(string policyName, IEnumerable<string> claims)
        : base(
            new[]
            {
                (Claims.Administrative.All, null),
                (Claims.Administrative.ModPack, null),
                (Claims.ResourceBased.ModPack.All, RouteParameterName),
            }.Concat(claims.Select((claim) => (claim, RouteParameterName))!))
    {
        PolicyName = policyName;
    }
}