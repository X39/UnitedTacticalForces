using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the administrative server permission.
/// </summary>
public sealed class AdministrativeServerRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="AdministrativeServerRequirement"/>.
    /// </summary>
    public AdministrativeServerRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.Server, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Administrative.Server;
}