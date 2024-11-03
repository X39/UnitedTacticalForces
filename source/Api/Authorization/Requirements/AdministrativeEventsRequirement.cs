using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative events permission.
/// </summary>
public sealed class AdministrativeEventsRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeEventsRequirement"/>.
    /// </summary>
    public AdministrativeEventsRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Event, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.Event;
}