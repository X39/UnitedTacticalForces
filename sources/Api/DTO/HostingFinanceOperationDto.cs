using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Retry-safe append-only correction or reversal and the ledger entries it appended atomically.
/// </summary>
public sealed record HostingFinanceOperationDto(
    Guid OperationIdentifier,
    EHostingFinanceOperationKind Kind,
    Guid? DeploymentId,
    Guid? SourceCostOwnerId,
    Guid? DestinationCostOwnerId,
    Guid? TargetLedgerEntryId,
    Guid? TargetFinanceOperationId,
    decimal Amount,
    string Currency,
    Instant TimeStampEffective,
    Instant TimeStampRecorded,
    Guid RecordedById,
    string Reason,
    IReadOnlyList<HostingFinanceAffectedLedgerEntryDto> AffectedLedgerEntries,
    IReadOnlyList<HostingLedgerEntryDto> Entries);
