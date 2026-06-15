namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Retry-safe linked reversal of one mistaken manual ledger entry or its balanced correction operation.
/// </summary>
public sealed record HostingLedgerReversalPayload(
    Guid OperationIdentifier,
    Instant? TimeStampEffective,
    string Reason);
