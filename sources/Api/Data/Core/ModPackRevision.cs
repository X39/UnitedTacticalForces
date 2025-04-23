using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Core;

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
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// The HTML content of this revision.
    /// </summary>
    [SuppressMessage("ReSharper", "EntityFramework.ModelValidation.UnlimitedStringLength")]
    public string Html { get; set; } = string.Empty;

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

    /// <summary>
    /// The definitions where this revision is part of.
    /// </summary>
    [InverseProperty(nameof(ModPackDefinition.ModPackRevisions))]
    public ICollection<ModPackDefinition>? ModPackDefinitions { get; set; }
}
