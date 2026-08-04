using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for inspecting all hosting-finance positions.
/// </summary>
public sealed class FinanceViewAllRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates the independently assignable global finance-read policy.
    /// </summary>
    public FinanceViewAllRequirement()
        : base([(Claims.Finance.All, null), (Claims.Finance.ViewAll, null)])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.ViewAll;
}
