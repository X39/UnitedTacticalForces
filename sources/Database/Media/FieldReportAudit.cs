using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Media;

/// <summary>
/// Represents an audit record for a field report mutation.
/// </summary>
[Index(nameof(FieldReportFk), nameof(TimeStampCreated), AllDescending = true)]
public class FieldReportAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Field report this audit record belongs to.
    /// </summary>
    [ForeignKey(nameof(FieldReportFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public FieldReport? FieldReport { get; set; }

    /// <summary>
    /// Foreign key to <see cref="FieldReport"/>.
    /// </summary>
    public Guid FieldReportFk { get; set; }

    /// <summary>
    /// User who performed the action.
    /// </summary>
    [ForeignKey(nameof(UserForeignKey))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    /// <summary>
    /// Foreign key to <see cref="User"/>.
    /// </summary>
    public Guid UserForeignKey { get; set; }

    /// <summary>
    /// Action performed on the field report.
    /// </summary>
    public EFieldReportAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise mutation metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
