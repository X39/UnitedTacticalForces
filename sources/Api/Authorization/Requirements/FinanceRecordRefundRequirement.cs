using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for recording an externally completed refund or prepaid-credit withdrawal.
/// </summary>
public sealed class FinanceRecordRefundRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    public FinanceRecordRefundRequirement()
        : base(
        [
            (Claims.Finance.All, null),
            (Claims.Finance.RecordRefund, null),
        ])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.RecordRefund;
}
