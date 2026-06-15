using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative user permission.
/// </summary>
public sealed class AdministrativeRoleRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeRoleRequirement"/>.
    /// </summary>
    public AdministrativeRoleRequirement()
        : base(
        [
            (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Role, null),
        ]
        )
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.Role;
}