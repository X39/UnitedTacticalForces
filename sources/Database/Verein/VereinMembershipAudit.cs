using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents an audit entry for formal membership changes.
/// </summary>
[Index(nameof(MembershipFk), nameof(TimeStampCreated), AllDescending = true)]
public class VereinMembershipAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Membership this audit entry belongs to.
    /// </summary>
    [ForeignKey(nameof(MembershipFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public VereinMembership? Membership { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Membership"/>.
    /// </summary>
    public long MembershipFk { get; set; }

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
    /// Action performed on the membership.
    /// </summary>
    public EVereinMembershipAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise mutation metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
