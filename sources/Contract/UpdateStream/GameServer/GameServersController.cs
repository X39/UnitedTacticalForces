using Microsoft.Extensions.Logging;

namespace X39.UnitedTacticalForces.Contract.UpdateStream.GameServer;

public class LogMessage
{
    /// <summary>
    /// The log level of the message.
    /// </summary>
    public LogLevel LogLevel { get; set; }
    /// <summary>
    /// The time-stamp this message was created.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// Contents of the message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The source of this log message.
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Foreign key of GameServer".
    /// </summary>
    public long GameServerId { get; set; }
}