using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents one manual Kassenbuch ledger entry.
/// </summary>
[Index(nameof(IsDeleted), nameof(TimeStampBooked))]
[Index(nameof(Category))]
public class KassenbuchEntry : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Date the ledger entry belongs to.
    /// </summary>
    public Instant TimeStampBooked { get; set; }

    /// <summary>
    /// Whether the entry is income or expense.
    /// </summary>
    public EKassenbuchEntryDirection Direction { get; set; }

    /// <summary>
    /// Positive amount in <see cref="Currency"/>.
    /// </summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO currency code.
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Ledger category.
    /// </summary>
    [MaxLength(96)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable booking description.
    /// </summary>
    [MaxLength(512)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional external reference.
    /// </summary>
    [MaxLength(128)]
    public string? Reference { get; set; }

    /// <summary>
    /// Optional internal voucher/document number.
    /// </summary>
    [MaxLength(128)]
    public string? DocumentNumber { get; set; }

    /// <summary>
    /// Whether this entry was removed from the active ledger.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who created the entry.
    /// </summary>
    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="CreatedBy"/>.
    /// </summary>
    public Guid? CreatedByFk { get; set; }

    /// <summary>
    /// User who last updated the entry.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <summary>
    /// Audit records for this ledger entry.
    /// </summary>
    public ICollection<KassenbuchEntryAudit>? Audits { get; set; }
}
