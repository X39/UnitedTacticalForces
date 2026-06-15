using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the wiki rename permission.
/// </summary>
public sealed class WikiRenameRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="WikiRenameRequirement"/>.
    /// </summary>
    public WikiRenameRequirement()
        : base(
        [
            (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Wiki, null),
                (Claims.Wiki.All, null),
                (Claims.Wiki.Rename, null),
        ]
        )
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Wiki.Rename;
}
