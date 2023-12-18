﻿using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the wiki modify permission.
/// </summary>
public sealed class WikiDeleteRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="WikiDeleteRequirement"/>.
    /// </summary>
    public WikiDeleteRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Wiki, null),
                (Claims.Wiki.All, null),
                (Claims.Wiki.Delete, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Wiki.Delete;
}