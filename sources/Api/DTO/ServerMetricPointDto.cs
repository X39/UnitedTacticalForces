namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One bucket of the metric history of a server.
/// </summary>
/// <remarks>
/// A null aggregate means the worker reported no value for that metric in this bucket; it never means zero. A
/// bucket the worker never reported has no point at all, so a consumer breaks its line instead of interpolating.
/// </remarks>
public sealed record ServerMetricPointDto
{
    /// <summary>
    /// Gets the inclusive start of the bucket.
    /// </summary>
    /// <value>The API receipt time floored to the bucket resolution.</value>
    public required Instant BucketStart { get; init; }

    /// <summary>
    /// Gets the CPU percentage aggregate.
    /// </summary>
    /// <value>The aggregate, or <see langword="null"/> when no sample of the bucket carried a CPU value.</value>
    public ServerMetricAggregateDto? Cpu { get; init; }

    /// <summary>
    /// Gets the used memory aggregate in bytes.
    /// </summary>
    /// <value>The aggregate, or <see langword="null"/> when no sample of the bucket carried a memory value.</value>
    public ServerMetricAggregateDto? MemoryUsed { get; init; }

    /// <summary>
    /// Gets the connected player count aggregate.
    /// </summary>
    /// <value>The aggregate, or <see langword="null"/> when no sample of the bucket carried a player count.</value>
    public ServerMetricAggregateDto? Players { get; init; }
}
