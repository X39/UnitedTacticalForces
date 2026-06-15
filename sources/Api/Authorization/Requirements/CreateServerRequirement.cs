using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for creating a durable game-server record.
/// </summary>
public sealed class CreateServerRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    public CreateServerRequirement()
        : base(
        [
            (Claims.Administrative.All, null),
            (Claims.Administrative.Server, null),
            (Claims.Administrative.Hosting, null),
            (Claims.Creation.Server, null),
        ])
    {
    }

    public override string PolicyName { get; } = Claims.Creation.Server;
}
