namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// Settings of server metrics ingest, freshness and history retention, bound from the <c>Hosting:Metrics</c>
/// section.
/// </summary>
public sealed class ServerMetricsOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Hosting:Metrics";

    /// <summary>
    /// Gets or sets how often a worker reports a sample.
    /// </summary>
    public TimeSpan ReportInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets how many missed report intervals make the latest sample stale. At least two, so one late
    /// report never flips the reading.
    /// </summary>
    public int StaleAfterIntervals { get; set; } = 3;

    /// <summary>
    /// Gets or sets how far a worker-reported sampling time may lie in the future of the receipt time before the
    /// sample is rejected.
    /// </summary>
    public TimeSpan MaxSampleSkew { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long the 15 second rollup rows are kept.
    /// </summary>
    public TimeSpan RawRetention { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Gets or sets how long the 60 second rollup rows are kept.
    /// </summary>
    public TimeSpan MinuteRetention { get; set; } = TimeSpan.FromHours(26);

    /// <summary>
    /// Gets or sets how long the 300 second rollup rows are kept.
    /// </summary>
    public TimeSpan FiveMinuteRetention { get; set; } = TimeSpan.FromDays(7) + TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets how often expired rollup rows are pruned.
    /// </summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets how old a sample may be at receipt, and how old the latest sample may be when read, before it counts
    /// as stale.
    /// </summary>
    /// <value>The report interval multiplied by the number of stale intervals.</value>
    public TimeSpan StaleAfter => ReportInterval * StaleAfterIntervals;

    /// <summary>
    /// Checks the options for unusable values.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// An interval or retention is not positive, <see cref="StaleAfterIntervals"/> is below two, or a retention is
    /// shorter than the span its tier serves: raw below one hour, minute below 24 hours, five-minute below
    /// seven days.
    /// </exception>
    public void Validate()
    {
        if (ReportInterval <= TimeSpan.Zero
            || StaleAfterIntervals < 2
            || MaxSampleSkew <= TimeSpan.Zero
            || PruneInterval <= TimeSpan.Zero
            || RawRetention <= TimeSpan.Zero
            || MinuteRetention <= TimeSpan.Zero
            || FiveMinuteRetention <= TimeSpan.Zero
            || RawRetention < TimeSpan.FromHours(1)
            || MinuteRetention < TimeSpan.FromHours(24)
            || FiveMinuteRetention < TimeSpan.FromDays(7))
        {
            throw new InvalidOperationException("Hosting:Metrics contains invalid settings.");
        }
    }
}
