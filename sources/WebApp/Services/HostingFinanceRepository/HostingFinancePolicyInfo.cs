namespace X39.UnitedTacticalForces.WebApp.Services.HostingFinanceRepository;

public sealed record HostingFinancePolicyInfo
{
    public decimal MinimumSafetyRangeEur { get; init; }
    public int ReservationSliceMinutes { get; init; }
    public DateTimeOffset? TimeStampUpdated { get; init; }
    public Guid? UpdatedById { get; init; }
}
