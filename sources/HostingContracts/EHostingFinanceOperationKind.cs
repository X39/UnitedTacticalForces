namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Groups append-only finance mutations that require more than one ledger entry or an explicit target link.
/// </summary>
public enum EHostingFinanceOperationKind
{
    DeploymentCostOwnerCorrection = 0,
    LedgerReversal = 1,
}
