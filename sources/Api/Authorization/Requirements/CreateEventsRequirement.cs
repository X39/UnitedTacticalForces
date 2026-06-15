using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the CreateEvents permission.
/// </summary>
public sealed class CreateEventsRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="CreateEventsRequirement"/>.
    /// </summary>
    public CreateEventsRequirement()
        : base(
        [
            (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Event, null),
                (Claims.Creation.Events, null),
        ]
        )
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Creation.Events;
}