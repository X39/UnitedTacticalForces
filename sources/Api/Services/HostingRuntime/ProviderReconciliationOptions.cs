namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Scheduling and retention settings for provider-wide resource reconciliation.
/// </summary>
public sealed class ProviderReconciliationOptions
{
    public const string SectionName = "Hosting:ProviderReconciliation";

    public bool Enabled { get; set; } = true;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan CompletedIncidentRetention { get; set; } = TimeSpan.FromDays(365);

    internal void Validate()
    {
        if (InitialDelay < TimeSpan.Zero
            || ScanInterval <= TimeSpan.Zero
            || CompletedIncidentRetention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{SectionName} contains invalid scheduling or retention durations.");
        }
    }
}
