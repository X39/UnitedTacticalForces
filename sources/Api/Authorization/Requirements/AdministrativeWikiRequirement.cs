using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative wiki permission.
/// </summary>
public sealed class AdministrativeWikiRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeWikiRequirement"/>.
    /// </summary>
    public AdministrativeWikiRequirement()
        : base(
        [
            (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Wiki, null),
        ]
        )
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.Wiki;
}