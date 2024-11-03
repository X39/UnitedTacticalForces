using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the create role permission.
/// </summary>
public sealed class CreateRoleRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="CreateRoleRequirement"/>.
    /// </summary>
    public CreateRoleRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Role, null),
                (Claims.Creation.Role, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Creation.Role;
}