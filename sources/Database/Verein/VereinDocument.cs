using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents a versioned Verein governance document.
/// </summary>
[Index(nameof(Kind), nameof(IsDeleted))]
public class VereinDocument : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// The document kind.
    /// </summary>
    public EVereinDocumentKind Kind { get; set; }

    /// <summary>
    /// Whether this document is removed from active workflows.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who created the document.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid? CreatedByFk { get; set; }

    /// <summary>
    /// User who last updated the document.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <summary>
    /// Revisions belonging to this document.
    /// </summary>
    public ICollection<VereinDocumentRevision>? Revisions { get; set; }

    /// <summary>
    /// Audit records for this document.
    /// </summary>
    public ICollection<VereinDocumentAudit>? Audits { get; set; }
}
