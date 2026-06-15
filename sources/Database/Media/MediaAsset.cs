using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Media;

/// <summary>
/// Represents one reusable image asset managed through the content gallery.
/// </summary>
[Index(nameof(Sha256Hash), IsUnique = true)]
[Index(nameof(TimeStampCreated))]
public class MediaAsset : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Original client supplied file name.
    /// </summary>
    [MaxLength(256)]
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Normalized image MIME type.
    /// </summary>
    [MaxLength(64)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Stored file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Storage-local file name. This value is never returned to frontend clients.
    /// </summary>
    [MaxLength(96)]
    public string StorageFileName { get; set; } = string.Empty;

    /// <summary>
    /// Lower-case SHA-256 hash used to deduplicate uploaded image bytes.
    /// </summary>
    [MaxLength(64)]
    public string Sha256Hash { get; set; } = string.Empty;

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last metadata update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who first uploaded the asset, when available.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid? CreatedByFk { get; set; }

    /// <summary>
    /// Tags assigned to the media asset.
    /// </summary>
    public ICollection<MediaAssetTag>? Tags { get; set; }
}
