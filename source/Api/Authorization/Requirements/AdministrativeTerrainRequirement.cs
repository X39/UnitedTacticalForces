using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative terrain permission.
/// </summary>
public sealed class AdministrativeTerrainRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeTerrainRequirement"/>.
    /// </summary>
    public AdministrativeTerrainRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Terrain, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.Terrain;
}