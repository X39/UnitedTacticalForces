using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents a formal Vereinsmitgliedschaft separate from account verification.
/// </summary>
[Index(nameof(UserFk), IsUnique = true)]
[Index(nameof(Status))]
[Index(nameof(MemberNumber), IsUnique = true)]
public class VereinMembership : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// User this formal membership belongs to.
    /// </summary>
    [ForeignKey(nameof(UserFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    /// <summary>
    /// Foreign key to <see cref="User"/>.
    /// </summary>
    public Guid UserFk { get; set; }

    /// <summary>
    /// Optional association member number.
    /// </summary>
    [MaxLength(64)]
    public string? MemberNumber { get; set; }

    /// <summary>
    /// Current formal membership status.
    /// </summary>
    public EVereinMembershipStatus Status { get; set; }

    /// <summary>
    /// Date at which membership became active or was recorded as starting.
    /// </summary>
    public Instant TimeStampStarted { get; set; }

    /// <summary>
    /// Date at which membership ended, if applicable.
    /// </summary>
    public Instant? TimeStampEnded { get; set; }

    /// <summary>
    /// Administrative notes visible only to Verein managers.
    /// </summary>
    [MaxLength(2048)]
    public string Notes { get; set; } = string.Empty;

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who created the membership record.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid? CreatedByFk { get; set; }

    /// <summary>
    /// User who last updated the membership record.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <summary>
    /// Audit records for this membership.
    /// </summary>
    public ICollection<VereinMembershipAudit>? Audits { get; set; }
}
