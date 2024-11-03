using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// The base class for terrain id requirements.
/// </summary>
[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public abstract class TerrainIdRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// The name of the route parameter to check against.
    /// </summary>
    public const string RouteParameterName = "terrainId";

    /// <inheritdoc />
    public override string PolicyName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="TerrainIdRequirement"/>.
    /// </summary>
    /// <param name="claim">The claim required to have the value of the route parameter.</param>
    protected TerrainIdRequirement(string claim)
        : base(
            new[]
            {
                (Claims.Administrative.All, null),
                (Claims.Administrative.Terrain, null),
                (Claims.ResourceBased.Terrain.All, RouteParameterName),
                (claim, RouteParameterName),
            })
    {
        PolicyName = claim;
    }

    /// <summary>
    /// Creates a new instance of <see cref="TerrainIdRequirement"/>.
    /// </summary>
    /// <param name="policyName">The name of the policy to register this requirement to.</param>
    /// <param name="claims">The claims required to have the value of the route parameter.</param>
    protected TerrainIdRequirement(string policyName, IEnumerable<string> claims)
        : base(
            new[]
            {
                (Claims.Administrative.All, null),
                (Claims.Administrative.Terrain, null),
                (Claims.ResourceBased.Terrain.All, RouteParameterName),
            }.Concat(claims.Select((claim) => (claim, RouteParameterName))!))
    {
        PolicyName = policyName;
    }
}