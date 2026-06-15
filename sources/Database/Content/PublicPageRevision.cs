using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Database.Media;

namespace X39.UnitedTacticalForces.Database.Content;

/// <summary>
/// Represents one markdown revision of a public page.
/// </summary>
[Index(nameof(PageFk), nameof(Version), IsUnique = true)]
[Index(nameof(PageFk), nameof(IsPublished), nameof(TimeStampPublished))]
public class PublicPageRevision : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Page this revision belongs to.
    /// </summary>
    [ForeignKey(nameof(PageFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public PublicPage? Page { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Page"/>.
    /// </summary>
    public long PageFk { get; set; }

    /// <summary>
    /// Monotonic page-local revision number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Display title.
    /// </summary>
    [MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Short page summary.
    /// </summary>
    [MaxLength(512)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Small label rendered above the hero title.
    /// </summary>
    [MaxLength(96)]
    public string HeroKicker { get; set; } = string.Empty;

    /// <summary>
    /// Hero title.
    /// </summary>
    [MaxLength(180)]
    public string HeroTitle { get; set; } = string.Empty;

    /// <summary>
    /// Hero description.
    /// </summary>
    [MaxLength(512)]
    public string HeroDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gallery media asset used as hero background image.
    /// </summary>
    [ForeignKey(nameof(HeroImageAssetFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? HeroImageAsset { get; set; }

    /// <summary>
    /// Foreign key to <see cref="HeroImageAsset"/>.
    /// </summary>
    public Guid HeroImageAssetFk { get; set; }

    /// <summary>
    /// Markdown body.
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>
    /// Optional structured content payload used by template-backed public pages.
    /// </summary>
    public string ContentJson { get; set; } = string.Empty;

    /// <summary>
    /// Revision comment, similar to a commit message.
    /// </summary>
    [MaxLength(512)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Whether this revision is visible to public readers.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Publication timestamp for public ordering.
    /// </summary>
    public Instant? TimeStampPublished { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// User who authored this revision.
    /// </summary>
    [ForeignKey(nameof(AuthorFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? Author { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Author"/>.
    /// </summary>
    public Guid? AuthorFk { get; set; }
}
