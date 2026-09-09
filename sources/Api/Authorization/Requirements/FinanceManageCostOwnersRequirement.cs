using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requires permission to create or rename hosting-finance cost owners.
/// </summary>
public sealed class FinanceManageCostOwnersRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Initializes the independently assignable cost-owner administration policy.
    /// </summary>
    public FinanceManageCostOwnersRequirement()
        : base([(Claims.Finance.All, null), (Claims.Finance.ManageCostOwners, null)])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.ManageCostOwners;
}
