namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Data Transfer Object (DTO) for game server logs.
/// </summary>
/// <remarks>
/// This DTO encapsulates information about a single log entry from a game server,
/// including the log's level, timestamp, message, and source, as well as the foreign key
/// of the associated game server.
/// </remarks>
public record PlainGameServerLogDto
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// The log level of the message.
    /// </summary>
    public LogLevel LogLevel { get; init; }

    /// <summary>
    /// The time-stamp this message was created.
    /// </summary>
    public DateTimeOffset TimeStamp { get; init; }

    /// <summary>
    /// Contents of the message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The source of this log message.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Foreign key of GameServer.
    /// </summary>
    public long GameServerFk { get; init; }
}
