using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Media;

/// <summary>
/// Represents a public field report post shown on the community site.
/// </summary>
[Index(nameof(Slug), IsUnique = true)]
[Index(nameof(IsPublished), nameof(IsDeleted), nameof(TimeStampPublished))]
[Index(nameof(IsFeatured), nameof(SortOrder), nameof(TimeStampPublished))]
public class FieldReport : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// URL-safe public identifier.
    /// </summary>
    [MaxLength(120)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Display title.
    /// </summary>
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Short public summary.
    /// </summary>
    [MaxLength(320)]
    public string Excerpt { get; set; } = string.Empty;

    /// <summary>
    /// Editorial category label.
    /// </summary>
    [MaxLength(64)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Markdown body rendered on the public detail page.
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>
    /// Optional gallery media asset used as hero image.
    /// </summary>
    [ForeignKey(nameof(HeroImageAssetFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? HeroImageAsset { get; set; }

    /// <summary>
    /// Foreign key to <see cref="HeroImageAsset"/>.
    /// </summary>
    public Guid? HeroImageAssetFk { get; set; }

    /// <summary>
    /// Alternative text for the hero image.
    /// </summary>
    [MaxLength(200)]
    public string? HeroImageAlt { get; set; }

    /// <summary>
    /// Whether this report should be promoted in public feature slots.
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Manual ordering value for public lists.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this report is visible publicly.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Publication timestamp. Future values are hidden from public readers until reached.
    /// </summary>
    public Instant? TimeStampPublished { get; set; }

    /// <summary>
    /// Whether this report is removed from the active editorial workflow.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User that created the report.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid CreatedByFk { get; set; }

    /// <summary>
    /// User that last updated the report.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid UpdatedByFk { get; set; }

    /// <summary>
    /// Audit records for this field report.
    /// </summary>
    public ICollection<FieldReportAudit>? Audits { get; set; }
}
