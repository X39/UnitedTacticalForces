using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the create modpack permission.
/// </summary>
public sealed class CreateModPackRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates a new instance of <see cref="CreateServerRequirement"/>.
    /// </summary>
    public CreateModPackRequirement()
        : base(
            new[]
            {
                (Claims.Administrative.All, default(string?)),
                (Claims.Administrative.ModPack, null),
                (Claims.Creation.ModPack, null),
            })
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Creation.ModPack;
}