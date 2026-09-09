using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Database.Authority;

namespace X39.UnitedTacticalForces.Database.Core;

/// <summary>
/// A modpack revision is a revision of a modpack definition.
/// </summary>
public class ModPackRevision
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// The timestamp when this revision was created.
    /// </summary>
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// The user who updated this revision.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// The foreign key of the user who updated this revision.
    /// </summary>
    public Guid UpdatedByFk { get; set; }

    /// <summary>
    /// Whether this revision is active or not.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Meta data for users.
    /// </summary>
    public ICollection<UserModPackMeta>? UserMetas { get; set; }

    /// <summary>
    /// The owning definition of this revision.
    /// </summary>
    [ForeignKey(nameof(DefinitionFk))]
    public ModPackDefinition? Definition { get; set; }

    /// <summary>
    /// Foreign key of the owning definition of this revision.
    /// </summary>
    public long DefinitionFk { get; set; }

    /// <summary>Gets or sets the ordered direct mod memberships.</summary>
    public ICollection<ModPackRevisionMod> Mods { get; set; } = [];

    /// <summary>Gets or sets the ordered nested mod-pack memberships.</summary>
    public ICollection<ModPackRevisionDependency> Dependencies { get; set; } = [];
}
