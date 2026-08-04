using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.WebApp.Services.HostingFinanceRepository;

public sealed record HostingCostOwnerInfo
{
    public Guid CostOwnerId { get; init; }
    public ECostOwnerKind Kind { get; init; }
    public Guid? UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool IsFrozen { get; init; }
    public decimal Balance { get; init; }
    public decimal? Allowance { get; init; }
    public decimal ReservedProviderCost { get; init; }
    public decimal SpendableAmount { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTimeOffset TimeStampUpdated { get; init; }
    public IReadOnlyList<HostingLedgerEntryInfo> RecentLedgerEntries { get; init; } = [];
}
