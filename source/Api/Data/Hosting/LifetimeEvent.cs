using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api.Data.Hosting;

/// <summary>
/// Represents lifetime events for a <see cref="Hosting.GameServer"/>.
/// </summary>
public class LifetimeEvent
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key] public long PrimaryKey { get; set; }
    
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
    [ForeignKey(nameof(ExecutedByFk))]
    public User? ExecutedBy { get; set; }
    
    /// <summary>
    /// Foreign key of <see cref="ExecutedBy"/>.
    /// </summary>
    public Guid? ExecutedByFk { get; set; }
    
    /// <summary>
    /// Timestamp when this event was registred.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// The status changed into.
    /// </summary>
    public ELifetimeStatus Status { get; set; }
}