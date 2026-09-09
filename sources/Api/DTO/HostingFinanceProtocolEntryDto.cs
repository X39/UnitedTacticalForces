using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Presents one append-only hosting-finance ledger entry with related operational context.
/// </summary>
public sealed record HostingFinanceProtocolEntryDto
{
    /// <summary>Gets the ledger-entry identifier.</summary>
    public Guid Id { get; init; }
    /// <summary>Gets the cost-owner identifier.</summary>
    public Guid CostOwnerId { get; init; }
    /// <summary>Gets the current cost-owner display name.</summary>
    public string CostOwnerDisplayName { get; init; } = string.Empty;
    /// <summary>Gets the cost-owner kind.</summary>
    public ECostOwnerKind CostOwnerKind { get; init; }
    /// <summary>Gets the related deployment identifier.</summary>
    public Guid? DeploymentId { get; init; }
    /// <summary>Gets the related server identifier.</summary>
    public Guid? ServerId { get; init; }
    /// <summary>Gets the related server title.</summary>
    public string? ServerTitle { get; init; }
    /// <summary>Gets the ledger-entry kind.</summary>
    public EHostingLedgerEntryKind Kind { get; init; }
    /// <summary>Gets the signed balance delta.</summary>
    public decimal Amount { get; init; }
    /// <summary>Gets the balance after the entry.</summary>
    public decimal BalanceAfter { get; init; }
    /// <summary>Gets the entry currency.</summary>
    public string Currency { get; init; } = "EUR";
    /// <summary>Gets the effective booking time.</summary>
    public Instant TimeStampEffective { get; init; }
    /// <summary>Gets the recorded timestamp.</summary>
    public Instant TimeStampRecorded { get; init; }
    /// <summary>Gets the recording actor identifier.</summary>
    public Guid? RecordedById { get; init; }
    /// <summary>Gets the recording actor display name.</summary>
    public string? RecordedByDisplayName { get; init; }
    /// <summary>Gets the retry-safe record identifier.</summary>
    public Guid? RecordIdentifier { get; init; }
    /// <summary>Gets the containing finance-operation identifier.</summary>
    public Guid? FinanceOperationId { get; init; }
    /// <summary>Gets the original entry negated by this entry.</summary>
    public Guid? ReversedEntryId { get; init; }
    /// <summary>Gets an entry that has already reversed this entry.</summary>
    public Guid? ReversalEntryId { get; init; }
    /// <summary>Gets whether a new reversal may be requested.</summary>
    public bool IsReversible { get; init; }
    /// <summary>Gets the external accounting reference.</summary>
    public string? ExternalReference { get; init; }
    /// <summary>Gets the recorded finance reason.</summary>
    public string? Reason { get; init; }
    /// <summary>Gets the provider identifier for a provider-cost slice.</summary>
    public string? ProviderIdentifier { get; init; }
    /// <summary>Gets the estimated hourly rate for a provider-cost slice.</summary>
    public decimal? EstimatedHourlyRate { get; init; }
    /// <summary>Gets the provider-cost slice start.</summary>
    public Instant? SliceStartsAt { get; init; }
    /// <summary>Gets the provider-cost slice end.</summary>
    public Instant? SliceEndsAt { get; init; }
    /// <summary>Gets the previous finite allowance.</summary>
    public decimal? PreviousAllowance { get; init; }
    /// <summary>Gets the new finite allowance.</summary>
    public decimal? NewAllowance { get; init; }
    /// <summary>Gets the counterparty cost-owner identifier for a correction.</summary>
    public Guid? CounterpartyCostOwnerId { get; init; }
    /// <summary>Gets the counterparty display name for a correction.</summary>
    public string? CounterpartyDisplayName { get; init; }
}
