namespace X39.UnitedTacticalForces.WebApp.Services.HostingFinanceRepository;

public interface IHostingFinanceRepository
{
    Task<IReadOnlyList<HostingCostOwnerInfo>> GetCostOwnersAsync(CancellationToken cancellationToken = default);
    Task<HostingCostOwnerInfo?> GetUserCostOwnerAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<HostingCostOwnerInfo> EnsureUserCostOwnerAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<HostingCostOwnerInfo> RecordPaymentAsync(
        Guid costOwnerId, decimal amount, DateTimeOffset effectiveAt, string reference, string? reason,
        CancellationToken cancellationToken = default);
    Task<HostingCostOwnerInfo> ChangeAllowanceAsync(
        Guid costOwnerId, decimal? allowance, string reason, CancellationToken cancellationToken = default);
    Task<HostingFinancePolicyInfo> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<HostingFinancePolicyInfo> UpdatePolicyAsync(
        decimal minimumSafetyRangeEur, int reservationSliceMinutes,
        CancellationToken cancellationToken = default);
}
