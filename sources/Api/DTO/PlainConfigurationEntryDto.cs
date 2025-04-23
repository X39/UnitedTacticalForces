using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a plain configuration entry within the system.
/// This class is used to store configuration details including metadata and associations to other entities.
/// </summary>
public record PlainConfigurationEntryDto
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long PrimaryKey { get; set; }

    /// <summary>
    /// The filename for the configuration.
    /// </summary>
    [MaxLength(2048)]
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// The path for the configuration.
    /// </summary>
    [MaxLength(2048)]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The value held.
    /// </summary>
    [MaxLength(2048)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key of GameServer.
    /// </summary>
    public long GameServerFk { get; set; }

    /// <summary>
    /// Foreign key of ChangedBy User.
    /// </summary>
    public Guid ChangedByFk { get; set; }

    /// <summary>
    /// Timestamp when this entity was created.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// If an information is sensitive, it must be blanked prior to being received.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// If an information gets changed, the old one will stay but with "IsActive" false.
    /// </summary>
    public bool IsActive { get; set; }
}
