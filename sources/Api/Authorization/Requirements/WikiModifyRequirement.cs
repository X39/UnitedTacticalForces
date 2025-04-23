using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the wiki modify permission.
/// </summary>
public sealed class WikiModifyRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="WikiModifyRequirement"/>.
    /// </summary>
    public WikiModifyRequirement()
        : base(
        [
            (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Wiki, null),
                (Claims.Wiki.All, null),
                (Claims.Wiki.Modify, null),
        ]
        )
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Wiki.Modify;
}