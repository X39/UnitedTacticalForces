using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Core;

/// <summary>
/// A modpack definition is the base of a modpack.
/// It contains the title, the owner and the revisions of the modpack.
/// A modpack definition can have multiple revisions.
/// A modpack may be a composition of multiple modpack revisions.
/// </summary>
public class ModPackDefinition
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// The timestamp when this modpack was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; set; }
    /// <summary>
    /// The name of this modpack.
    /// </summary>
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
    /// Whether this modpack is a composition of multiple modpack revisions
    /// or not.
    /// </summary>
    public bool IsComposition { get; set; }
    
    /// <summary>
    /// The revisions which are part of this modpack.
    /// </summary>
    [InverseProperty(nameof(ModPackRevision.ModPackDefinitions))]
    public ICollection<ModPackRevision>? ModPackRevisions { get; set; }
    
    /// <summary>
    /// The revisions which are owned by this modpack.
    /// </summary>
    [InverseProperty(nameof(ModPackRevision.Definition))]
    public ICollection<ModPackRevision>? ModPackRevisionsOwned { get; set; }
}