using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Current hosting-finance position for one cost owner.
/// </summary>
public sealed record HostingCostOwnerDto(
    Guid CostOwnerId,
    ECostOwnerKind Kind,
    Guid? UserId,
    string DisplayName,
    bool IsFrozen,
    decimal Balance,
    decimal? Allowance,
    decimal ReservedProviderCost,
    decimal SpendableAmount,
    string Currency,
    Instant TimeStampUpdated,
    IReadOnlyList<HostingLedgerEntryDto> RecentLedgerEntries);
