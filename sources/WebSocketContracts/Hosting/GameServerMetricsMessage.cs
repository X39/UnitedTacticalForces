namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

/// <summary>
/// Pushes the current metric readings of one game server to the users who may view the server.
/// </summary>
/// <remarks>
/// Sent once after every stored sample of the primary worker, which is about every 15 seconds for a running server,
/// so the client never polls. The message carries only flat scalar properties because the update stream serializer
/// uses web defaults with reference preservation and has no NodaTime or enum converter; instants are
/// <see cref="DateTimeOffset"/> values. It travels only on per-user paths of the viewers of the server. Every
/// reading is either a value or exactly one reason code decided by the control plane; an unavailable reading is never
/// zero, and the client only re-ages a reading against <see cref="StaleAfterSeconds"/>.
/// </remarks>
public sealed class GameServerMetricsMessage
{
    /// <summary>
    /// Gets the server the readings belong to.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public required Guid ServerId { get; init; }

    /// <summary>
    /// Gets when the control plane received the readings.
    /// </summary>
    /// <value>
    /// The receipt time of the latest sample of the active session, serialized as an ISO-8601 string with offset,
    /// or <see langword="null"/> when no sample of the active session exists.
    /// </value>
    public DateTimeOffset? AsOf { get; init; }

    /// <summary>
    /// Gets how many seconds after <see cref="AsOf"/> the readings count as stale.
    /// </summary>
    /// <value>The whole seconds of the configured stale limit; 45 with the default settings.</value>
    public required int StaleAfterSeconds { get; init; }

    /// <summary>
    /// Gets when the game process of the primary worker started.
    /// </summary>
    /// <value>
    /// The process start, or <see langword="null"/> when <see cref="RunningSinceUnavailableReason"/> is set.
    /// </value>
    public DateTimeOffset? RunningSince { get; init; }

    /// <summary>
    /// Gets why <see cref="RunningSince"/> is unavailable.
    /// </summary>
    /// <value>A reason code, or <see langword="null"/> when <see cref="RunningSince"/> is set.</value>
    public string? RunningSinceUnavailableReason { get; init; }

    /// <summary>
    /// Gets the CPU load of the primary host.
    /// </summary>
    /// <value>The CPU percentage, or <see langword="null"/> when <see cref="CpuUnavailableReason"/> is set.</value>
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
    /// The used memory in bytes, or <see langword="null"/> when <see cref="MemoryUnavailableReason"/> is set.
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
    /// The player count where a measured zero stays zero, or <see langword="null"/> when
    /// <see cref="PlayersUnavailableReason"/> is set.
    /// </value>
    public int? PlayerCount { get; init; }

    /// <summary>
    /// Gets the player capacity of the game server.
    /// </summary>
    /// <value>The capacity reported next to <see cref="PlayerCount"/>, or <see langword="null"/> when unknown.</value>
    public int? PlayerCapacity { get; init; }

    /// <summary>
    /// Gets why <see cref="PlayerCount"/> is unavailable.
    /// </summary>
    /// <value>A reason code, or <see langword="null"/> when <see cref="PlayerCount"/> is set.</value>
    public string? PlayersUnavailableReason { get; init; }
}
