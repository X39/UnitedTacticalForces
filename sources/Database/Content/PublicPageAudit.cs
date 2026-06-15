using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Content;

/// <summary>
/// Represents an audit entry for public page changes.
/// </summary>
[Index(nameof(PageFk), nameof(TimeStampCreated), AllDescending = true)]
public class PublicPageAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Page this audit entry belongs to.
    /// </summary>
    [ForeignKey(nameof(PageFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public PublicPage? Page { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Page"/>.
    /// </summary>
    public long PageFk { get; set; }

    /// <summary>
    /// Optional revision involved in the action.
    /// </summary>
    [ForeignKey(nameof(RevisionFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public PublicPageRevision? Revision { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Revision"/>.
    /// </summary>
    public long? RevisionFk { get; set; }

    /// <summary>
    /// User who performed the action.
    /// </summary>
    [ForeignKey(nameof(UserForeignKey))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    /// <summary>
    /// Foreign key to <see cref="User"/>.
    /// </summary>
    public Guid? UserForeignKey { get; set; }

    /// <summary>
    /// Action performed on the page.
    /// </summary>
    public EPublicPageAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise mutation metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
