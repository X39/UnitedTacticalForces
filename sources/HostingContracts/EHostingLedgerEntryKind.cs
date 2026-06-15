namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Append-only hosting finance event types.
/// </summary>
public enum EHostingLedgerEntryKind
{
    ProviderCostSlice = 0,
    BankTransferPayment = 1,
    AllowanceChanged = 2,
    ExternalRefund = 3,
    DeploymentCostCorrectionCredit = 4,
    DeploymentCostCorrectionDebit = 5,
    LedgerReversal = 6,
}
