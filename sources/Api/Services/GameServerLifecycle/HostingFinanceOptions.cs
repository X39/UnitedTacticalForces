namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Fail-closed estimated provider-cost policy used before and during billable deployment allocation.
/// </summary>
public sealed class HostingFinanceOptions
{
    public const string SectionName = "Hosting:Finance";

    public decimal MinimumSafetyRangeEur { get; set; } = 5m;

    public TimeSpan ReservationSlice { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan ReconciliationInterval { get; set; } = TimeSpan.FromSeconds(30);

    public Dictionary<string, decimal> EstimatedHourlyCostEur { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public decimal GetHourlyRate(string providerIdentifier)
    {
        Validate();
        if (!EstimatedHourlyCostEur.TryGetValue(providerIdentifier, out var rate) || rate <= 0)
        {
            throw new InvalidOperationException(
                $"No positive conservative hourly cost estimate is configured for provider '{providerIdentifier}'.");
        }
        return rate;
    }

    public void Validate()
    {
        if (MinimumSafetyRangeEur < 0
            || ReservationSlice <= TimeSpan.Zero
            || ReservationSlice > TimeSpan.FromHours(24)
            || ReconciliationInterval <= TimeSpan.Zero
            || ReconciliationInterval >= ReservationSlice)
        {
            throw new InvalidOperationException("Hosting:Finance contains invalid safety or slice settings.");
        }
    }
}
