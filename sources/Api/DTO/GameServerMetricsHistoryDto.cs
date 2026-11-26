namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The retained metric history of one game server for one range, read from the durable rollup tier.
/// </summary>
/// <remarks>
/// Buckets the worker never reported are absent from <see cref="Points"/>; the API never fills a gap with zero or
/// an interpolated value, so a consumer breaks its line at a gap.
/// </remarks>
public sealed record GameServerMetricsHistoryDto
{
    /// <summary>
    /// Gets the game server the history belongs to.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid GameServerId { get; init; }

    /// <summary>
    /// Gets the range code that was served.
    /// </summary>
    /// <value>One of <c>1h</c>, <c>24h</c> or <c>7d</c>.</value>
    public required string Range { get; init; }

    /// <summary>
    /// Gets the inclusive start of the covered window.
    /// </summary>
    /// <value>The read time minus the range span.</value>
    public required Instant From { get; init; }

    /// <summary>
    /// Gets the end of the covered window.
    /// </summary>
    /// <value>The read time.</value>
    public required Instant To { get; init; }

    /// <summary>
    /// Gets the bucket length of the served tier in seconds.
    /// </summary>
    /// <value>15, 60 or 300.</value>
    public required int BucketSeconds { get; init; }

    /// <summary>
    /// Gets the total memory of the primary host, used to scale the memory series.
    /// </summary>
    /// <value>The newest non-null total memory in bytes of the range, or <see langword="null"/> when none exists.</value>
    public long? MemoryTotalBytes { get; init; }

    /// <summary>
    /// Gets the player capacity of the game server.
    /// </summary>
    /// <value>The newest non-null player capacity of the range, or <see langword="null"/> when none exists.</value>
    public int? PlayerCapacity { get; init; }

    /// <summary>
    /// Gets the reported buckets in ascending time order.
    /// </summary>
    /// <value>One point per bucket that received at least one sample; empty windows have no point.</value>
    public required IReadOnlyList<ServerMetricPointDto> Points { get; init; }
}
