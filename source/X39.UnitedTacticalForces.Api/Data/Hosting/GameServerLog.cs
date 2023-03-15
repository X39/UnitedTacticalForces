using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Api.Data.Hosting;

[PublicAPI]
[Index(nameof(TimeStamp))]
public class GameServerLog
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key]
    public long PrimaryKey { get; set; }
    
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
}