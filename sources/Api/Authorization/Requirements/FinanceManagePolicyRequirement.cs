using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for changing installation-wide hosting-finance policy.
/// </summary>
public sealed class FinanceManagePolicyRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates the independently assignable installation-finance-policy mutation policy.
    /// </summary>
    public FinanceManagePolicyRequirement()
        : base([(Claims.Finance.All, null), (Claims.Finance.ManagePolicy, null)])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.ManagePolicy;
}
