using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for changing a finite cost-owner allowance.
/// </summary>
public sealed class FinanceManageAllowanceRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates the independently assignable allowance-management policy.
    /// </summary>
    public FinanceManageAllowanceRequirement()
        : base(
        [
            (Claims.Finance.All, null),
            (Claims.Finance.ManageAllowance, null),
        ])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.ManageAllowance;
}
