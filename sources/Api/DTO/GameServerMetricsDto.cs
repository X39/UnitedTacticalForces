namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The current metric readings of one game server, decided by the control plane.
/// </summary>
/// <remarks>
/// Every reading is either a value or exactly one reason code from
/// <see cref="X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerMetricUnavailableReasons"/>; an
/// unavailable reading is never zero. The browser may only re-age a reading against
/// <see cref="StaleAfterSeconds"/>, it never decides on its own that a reading is unavailable.
/// </remarks>
public sealed record GameServerMetricsDto
{
    /// <summary>
    /// Gets the game server the readings belong to.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid GameServerId { get; init; }

    /// <summary>
    /// Gets when the readings were received by the control plane.
    /// </summary>
    /// <value>
    /// The receipt time of the latest sample of the active session, or <see langword="null"/> when no sample of the
    /// active session exists.
    /// </value>
    public Instant? AsOf { get; init; }

    /// <summary>
    /// Gets how many seconds after <see cref="AsOf"/> the readings count as stale.
    /// </summary>
    /// <value>The whole seconds of the configured stale limit; 45 with the default settings.</value>
    public required int StaleAfterSeconds { get; init; }

    /// <summary>
    /// Gets when the game process of the primary worker started.
    /// </summary>
    /// <value>
    /// The process start observed by the primary worker. Exactly one of this value and
    /// <see cref="RunningSinceUnavailableReason"/> is set.
    /// </value>
    public Instant? RunningSince { get; init; }

    /// <summary>
    /// Gets why <see cref="RunningSince"/> is unavailable.
    /// </summary>
    /// <value>A reason code, or <see langword="null"/> when <see cref="RunningSince"/> is set.</value>
    public string? RunningSinceUnavailableReason { get; init; }

    /// <summary>
    /// Gets the CPU load of the primary host.
    /// </summary>
    /// <value>
    /// The CPU percentage. Exactly one of this value and <see cref="CpuUnavailableReason"/> is set.
    /// </value>
    public double? CpuPercent { get; init; }

    /// <summary>
    /// Gets why <see cref="CpuPercent"/> is unavailable.
    /// </summary>
    /// <value>A reason code, or <see langword="null"/> when <see cref="CpuPercent"/> is set.</value>
    public string? CpuUnavailableReason { get; init; }

    /// <summary>
    /// Gets the used memory of the primary host.
    /// </summary>
    /// <value>
    /// The used memory in bytes. Set together with <see cref="MemoryTotalBytes"/>; exactly one of the pair and
    /// <see cref="MemoryUnavailableReason"/> is set.
    /// </value>
    public long? MemoryUsedBytes { get; init; }

    /// <summary>
    /// Gets the total memory of the primary host.
    /// </summary>
    /// <value>The total memory in bytes, set whenever <see cref="MemoryUsedBytes"/> is set.</value>
    public long? MemoryTotalBytes { get; init; }

    /// <summary>
    /// Gets why the memory reading is unavailable.
    /// </summary>
    /// <value>A reason code, or <see langword="null"/> when the memory reading is set.</value>
    public string? MemoryUnavailableReason { get; init; }

    /// <summary>
    /// Gets the number of connected players.
    /// </summary>
    /// <value>
    /// The player count as reported by the worker; a measured zero is returned as zero. Exactly one of this value
    /// and <see cref="PlayersUnavailableReason"/> is set.
    /// </value>
    public int? PlayerCount { get; init; }

    /// <summary>
    /// Gets the player capacity of the game server.
    /// </summary>
    /// <value>
    /// The capacity reported next to <see cref="PlayerCount"/>, or <see langword="null"/> when it is not known.
    /// </value>
    public int? PlayerCapacity { get; init; }

    /// <summary>
    /// Gets why <see cref="PlayerCount"/> is unavailable.
    /// </summary>
    /// <value>A reason code, or <see langword="null"/> when <see cref="PlayerCount"/> is set.</value>
    public string? PlayersUnavailableReason { get; init; }
}
