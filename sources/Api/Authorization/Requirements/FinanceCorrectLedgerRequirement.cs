using X39.UnitedTacticalForces.Api.Authorization.Abstraction;

namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for append-only ledger reversals and completed-deployment cost corrections.
/// </summary>
public sealed class FinanceCorrectLedgerRequirement : AnyClaimValuePairFromRouteRequirementBase
{
    public FinanceCorrectLedgerRequirement()
        : base(
        [
            (Claims.Finance.All, null),
            (Claims.Finance.CorrectLedger, null),
        ])
    {
    }

    /// <inheritdoc />
    public override string PolicyName { get; } = Claims.Finance.CorrectLedger;
}
