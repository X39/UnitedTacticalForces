using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the create server permission.
/// </summary>
public sealed class CreateServerRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="CreateServerRequirement"/>.
    /// </summary>
    public CreateServerRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Server, null),
                (Claims.Creation.Server, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Creation.Server;
}