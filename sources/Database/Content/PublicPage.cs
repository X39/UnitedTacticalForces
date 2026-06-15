using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Content;

/// <summary>
/// Represents a public, slug-addressed page that can be edited from the command center.
/// </summary>
[Index(nameof(Slug), IsUnique = true)]
[Index(nameof(IsDeleted))]
public class PublicPage : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Stable URL slug.
    /// </summary>
    [MaxLength(128)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Whether this page is removed from active workflows.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who created the page.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid? CreatedByFk { get; set; }

    /// <summary>
    /// User who last updated the page.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <summary>
    /// Revisions belonging to this page.
    /// </summary>
    public ICollection<PublicPageRevision>? Revisions { get; set; }

    /// <summary>
    /// Audit records for this page.
    /// </summary>
    public ICollection<PublicPageAudit>? Audits { get; set; }
}
