namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The average, minimum and maximum of one metric inside one history bucket, computed by the API.
/// </summary>
public sealed record ServerMetricAggregateDto
{
    /// <summary>
    /// Gets the average of the samples in the bucket.
    /// </summary>
    /// <value>The sum divided by the sample count, rounded to two decimals.</value>
    public required double Average { get; init; }

    /// <summary>
    /// Gets the lowest sample in the bucket.
    /// </summary>
    /// <value>The minimum, so the trough of a dip survives downsampling.</value>
    public required double Minimum { get; init; }

    /// <summary>
    /// Gets the highest sample in the bucket.
    /// </summary>
    /// <value>The maximum, so the peak of a spike survives downsampling.</value>
    public required double Maximum { get; init; }
}
