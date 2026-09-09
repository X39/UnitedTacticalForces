using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requires permission to freeze or unfreeze hosting-finance cost owners.
/// </summary>
public sealed class FinanceFreezeCostOwnerRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Initializes the independently assignable cost-owner freeze policy.
    /// </summary>
    public FinanceFreezeCostOwnerRequirement()
        : base([(Claims.Finance.All, null), (Claims.Finance.FreezeCostOwner, null)])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.FreezeCostOwner;
}
