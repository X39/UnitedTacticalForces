using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.WebApp.Services.HostingFinanceRepository;

public sealed record HostingLedgerEntryInfo
{
    public Guid Id { get; init; }
    public Guid CostOwnerId { get; init; }
    public Guid? DeploymentId { get; init; }
    public EHostingLedgerEntryKind Kind { get; init; }
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public decimal? PreviousAllowance { get; init; }
    public decimal? NewAllowance { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTimeOffset TimeStampEffective { get; init; }
    public DateTimeOffset TimeStampRecorded { get; init; }
    public Guid? RecordedById { get; init; }
    public Guid? RecordIdentifier { get; init; }
    public Guid? FinanceOperationId { get; init; }
    public Guid? ReversedEntryId { get; init; }
    public string? ExternalReference { get; init; }
    public string? Reason { get; init; }
}
