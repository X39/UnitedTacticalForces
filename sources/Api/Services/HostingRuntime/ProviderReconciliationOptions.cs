namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Scheduling and retention settings for provider-wide resource reconciliation.
/// </summary>
public sealed class ProviderReconciliationOptions
{
    public const string SectionName = "Hosting:ProviderReconciliation";

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Minimum time between the first and second successful provider-wide observations of an absent, issued create
    /// intent before teardown may close that intent. Later attributed resources remain subject to dangling cleanup.
    /// </summary>
    public TimeSpan AmbiguousCreateAbsenceConfirmationWindow { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan CompletedIncidentRetention { get; set; } = TimeSpan.FromDays(365);

    internal void Validate()
    {
        if (InitialDelay < TimeSpan.Zero
            || ScanInterval <= TimeSpan.Zero
            || AmbiguousCreateAbsenceConfirmationWindow < TimeSpan.Zero
            || CompletedIncidentRetention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{SectionName} contains invalid scheduling or retention durations.");
        }
    }

    internal void ValidateAgainstProviderSettlement(TimeSpan providerSettlementTimeout)
    {
        Validate();
        if (providerSettlementTimeout <= TimeSpan.Zero || providerSettlementTimeout > TimeSpan.FromMinutes(30))
        {
            throw new InvalidOperationException("Hosting:Worker:ProviderSettlementTimeout is invalid.");
        }
        var maximumClaimToSettlementInterval = providerSettlementTimeout + providerSettlementTimeout;
        if (AmbiguousCreateAbsenceConfirmationWindow <= maximumClaimToSettlementInterval)
        {
            throw new InvalidOperationException(
                $"{SectionName}:AmbiguousCreateAbsenceConfirmationWindow must be greater than twice "
                + "Hosting:Worker:ProviderSettlementTimeout so both the provider-call and returned-result windows close.");
        }
    }
}
