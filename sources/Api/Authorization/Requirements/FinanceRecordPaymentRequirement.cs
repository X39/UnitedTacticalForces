using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for recording an externally received payment. Finance capabilities remain independently assignable.
/// </summary>
public sealed class FinanceRecordPaymentRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    /// <summary>
    /// Creates the independently assignable payment-recording policy.
    /// </summary>
    public FinanceRecordPaymentRequirement()
        : base(
        [
            (Claims.Finance.All, null),
            (Claims.Finance.RecordPayment, null),
        ])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.RecordPayment;
}
