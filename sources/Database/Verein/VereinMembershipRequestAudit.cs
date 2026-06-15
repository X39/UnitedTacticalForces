using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents an audit entry for Verein membership request transitions.
/// </summary>
[Index(nameof(RequestFk), nameof(TimeStampCreated), AllDescending = true)]
public class VereinMembershipRequestAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Request this audit entry belongs to.
    /// </summary>
    [ForeignKey(nameof(RequestFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public VereinMembershipRequest? Request { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Request"/>.
    /// </summary>
    public long RequestFk { get; set; }

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
    /// Action performed on the request.
    /// </summary>
    public EVereinMembershipRequestAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise transition metadata without submitted PII.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
