namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// The history ranges a viewer can request; each range is served by exactly one rollup tier.
/// </summary>
public enum EServerMetricsRange
{
    /// <summary>
    /// The last hour, served by the 15 second tier.
    /// </summary>
    Hour = 0,

    /// <summary>
    /// The last 24 hours, served by the 60 second tier.
    /// </summary>
    Day = 1,

    /// <summary>
    /// The last 7 days, served by the 300 second tier.
    /// </summary>
    Week = 2,
}
