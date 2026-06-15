using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One append-only hosting finance entry returned after a manual mutation.
/// </summary>
public sealed record HostingLedgerEntryDto(
    Guid Id,
    Guid CostOwnerId,
    Guid? DeploymentId,
    EHostingLedgerEntryKind Kind,
    decimal Amount,
    decimal BalanceAfter,
    decimal? PreviousAllowance,
    decimal? NewAllowance,
    string Currency,
    Instant TimeStampEffective,
    Instant TimeStampRecorded,
    Guid? RecordedById,
    Guid? RecordIdentifier,
    Guid? FinanceOperationId,
    Guid? ReversedEntryId,
    string? ExternalReference,
    string? Reason);
