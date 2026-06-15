using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Media;

/// <summary>
/// Defines one public-site image slot backed by a reusable media asset.
/// </summary>
[Index(nameof(SortOrder))]
public class SiteImageSetting : IPrimaryKey<string>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    [MaxLength(128)]
    public string PrimaryKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label for command-center editors.
    /// </summary>
    [MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional command-center description.
    /// </summary>
    [MaxLength(512)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Editor grouping key used for page-specific public image settings routes.
    /// </summary>
    [MaxLength(96)]
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Display order within the editor group.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gallery media asset used by this public image slot.
    /// </summary>
    [ForeignKey(nameof(ImageAssetFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? ImageAsset { get; set; }

    /// <summary>
    /// Foreign key to <see cref="ImageAsset"/>.
    /// </summary>
    public Guid ImageAssetFk { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who last changed this image slot.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }
}
