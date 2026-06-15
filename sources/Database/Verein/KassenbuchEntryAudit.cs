using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents an audit entry for Kassenbuch mutations.
/// </summary>
[Index(nameof(KassenbuchEntryFk), nameof(TimeStampCreated), AllDescending = true)]
public class KassenbuchEntryAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Ledger entry this audit record belongs to.
    /// </summary>
    [ForeignKey(nameof(KassenbuchEntryFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public KassenbuchEntry? KassenbuchEntry { get; set; }

    /// <summary>
    /// Foreign key to <see cref="KassenbuchEntry"/>.
    /// </summary>
    public long KassenbuchEntryFk { get; set; }

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
    /// Action performed on the entry.
    /// </summary>
    public EKassenbuchAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise mutation metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
