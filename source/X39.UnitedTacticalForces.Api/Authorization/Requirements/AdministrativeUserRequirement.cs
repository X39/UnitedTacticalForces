﻿using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative user permission.
/// </summary>
public sealed class AdministrativeUserRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeUserRequirement"/>.
    /// </summary>
    public AdministrativeUserRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.User, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.User;
}