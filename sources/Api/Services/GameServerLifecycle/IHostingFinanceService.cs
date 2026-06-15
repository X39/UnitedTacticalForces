using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IHostingFinanceService
{
    Task<HostingCostLedgerEntry> RecordBankTransferPaymentAsync(
        Guid costOwnerId,
        Guid recordIdentifier,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string externalReference,
        string? reason,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<HostingCostLedgerEntry> ChangeAllowanceAsync(
        Guid costOwnerId,
        Guid recordIdentifier,
        decimal? allowance,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<HostingCostLedgerEntry> RecordExternalRefundAsync(
        Guid costOwnerId,
        Guid recordIdentifier,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string externalReference,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<HostingFinanceOperation> CorrectCompletedDeploymentCostOwnerAsync(
        Guid sourceCostOwnerId,
        Guid deploymentId,
        Guid operationIdentifier,
        Guid destinationCostOwnerId,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<HostingFinanceOperation> ReverseLedgerEntryAsync(
        Guid expectedCostOwnerId,
        Guid ledgerEntryId,
        Guid operationIdentifier,
        Instant timeStampEffective,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
