namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IHostingFinancePolicyService
{
    Task<EffectiveHostingFinancePolicy> GetAsync(CancellationToken cancellationToken = default);

    Task<EffectiveHostingFinancePolicy> UpdateAsync(
        decimal minimumSafetyRangeEur,
        int reservationSliceMinutes,
        Guid actorId,
        CancellationToken cancellationToken = default);
}

public sealed record EffectiveHostingFinancePolicy(
    decimal MinimumSafetyRangeEur,
    TimeSpan ReservationSlice,
    Instant? TimeStampUpdated,
    Guid? UpdatedById);
