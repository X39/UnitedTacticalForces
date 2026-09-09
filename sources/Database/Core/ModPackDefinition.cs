using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;

namespace X39.UnitedTacticalForces.Database.Core;

/// <summary>
/// A modpack definition is the base of a modpack.
/// It contains the title, the owner and the revisions of the modpack.
/// A modpack definition can have multiple revisions.
/// </summary>
[Index(nameof(GameIdentifier))]
public class ModPackDefinition
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Stable identifier of the game this mod pack belongs to.
    /// </summary>
    [MaxLength(64)]
    public string GameIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp when this modpack was created.
    /// </summary>
    public Instant TimeStampCreated { get; set; }
    /// <summary>
    /// The name of this modpack.
    /// </summary>
    [MaxLength(1024)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The owner of this modpack.
    /// </summary>
    [ForeignKey(nameof(OwnerFk))]
    public User? Owner { get; set; }

    /// <summary>
    /// The foreign key of the owner of this modpack.
    /// </summary>
    public Guid OwnerFk { get; set; }
    
    /// <summary>
    /// Whether this modpack is active or not.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The revisions which are owned by this modpack.
    /// </summary>
    [InverseProperty(nameof(ModPackRevision.Definition))]
    public ICollection<ModPackRevision>? ModPackRevisionsOwned { get; set; }
}
