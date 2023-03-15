using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Hosting;

/// <summary>
/// Represents configuration entries for a <see cref="Hosting.GameServer"/>.
/// </summary>
[Index(nameof(IsActive))]
[Index(nameof(Realm), nameof(Path))]
public class ConfigurationEntry
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key] public long PrimaryKey { get; set; }
    
    /// <summary>
    /// The filename for the configuration.
    /// </summary>
    public string Realm { get; set; } = string.Empty;
    
    /// <summary>
    /// The path for the configuration.
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// The value held.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// The related <see cref="Hosting.GameServer"/> that has changed its lifetime.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    public GameServer? GameServer { get; set; }
    
    /// <summary>
    /// Foreign key of <see cref="GameServer"/>.
    /// </summary>
    public long GameServerFk { get; set; }
    
    /// <summary>
    /// The user that caused the lifetime event.
    /// </summary>
    [ForeignKey(nameof(ChangedByFk))]
    public User? ChangedBy { get; set; }
    
    /// <summary>
    /// Foreign key of <see cref="ChangedBy"/>.
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