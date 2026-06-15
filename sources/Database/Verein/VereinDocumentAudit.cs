using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents an audit entry for Verein document changes.
/// </summary>
[Index(nameof(DocumentFk), nameof(TimeStampCreated), AllDescending = true)]
public class VereinDocumentAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Document this audit entry belongs to.
    /// </summary>
    [ForeignKey(nameof(DocumentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public VereinDocument? Document { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Document"/>.
    /// </summary>
    public long DocumentFk { get; set; }

    /// <summary>
    /// Optional revision involved in the action.
    /// </summary>
    [ForeignKey(nameof(RevisionFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public VereinDocumentRevision? Revision { get; set; }

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
    /// Action performed on the document.
    /// </summary>
    public EVereinDocumentAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise mutation metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
