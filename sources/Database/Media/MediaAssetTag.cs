using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Media;

/// <summary>
/// Represents one normalized tag assigned to a reusable media asset.
/// </summary>
[Index(nameof(MediaAssetFk), nameof(Value), IsUnique = true)]
[Index(nameof(Value))]
public class MediaAssetTag : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Asset this tag belongs to.
    /// </summary>
    [ForeignKey(nameof(MediaAssetFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public MediaAsset? MediaAsset { get; set; }

    /// <summary>
    /// Foreign key to <see cref="MediaAsset"/>.
    /// </summary>
    public Guid MediaAssetFk { get; set; }

    /// <summary>
    /// Normalized lower-case tag value.
    /// </summary>
    [MaxLength(64)]
    public string Value { get; set; } = string.Empty;

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// User who added the tag, when available.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid? CreatedByFk { get; set; }
}
