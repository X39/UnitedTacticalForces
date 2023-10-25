using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative mod pack permission.
/// </summary>
public sealed class AdministrativeModPackRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeModPackRequirement"/>.
    /// </summary>
    public AdministrativeModPackRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.ModPack, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.ModPack;
}