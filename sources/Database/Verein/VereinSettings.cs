using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Public Verein settings that are safe to show on the Join and Verein pages.
/// </summary>
[Index(nameof(Identifier), IsUnique = true)]
public class VereinSettings : IPrimaryKey<long>, ITimeStampCreated
{
    /// <summary>
    /// Singleton identifier for the public Verein settings row.
    /// </summary>
    public const string DefaultIdentifier = "default";

    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Stable singleton identifier.
    /// </summary>
    [MaxLength(64)]
    public string Identifier { get; set; } = DefaultIdentifier;

    /// <summary>
    /// Configured monthly contribution amount, if one is currently published.
    /// </summary>
    [Precision(18, 2)]
    public decimal? MembershipContributionMonthlyAmount { get; set; }

    /// <summary>
    /// Configured annual contribution amount, if one is currently published.
    /// </summary>
    [Precision(18, 2)]
    public decimal? MembershipContributionAnnualAmount { get; set; }

    /// <summary>
    /// Three-letter currency code for the contribution amounts.
    /// </summary>
    [MaxLength(3)]
    public string MembershipContributionCurrency { get; set; } = "EUR";

    /// <summary>
    /// German public explanation shown before membership application submission.
    /// </summary>
    [MaxLength(4096)]
    public string MembershipContributionPublicTextDe { get; set; } = string.Empty;

    /// <summary>
    /// English public explanation shown before membership application submission.
    /// </summary>
    [MaxLength(4096)]
    public string MembershipContributionPublicTextEn { get; set; } = string.Empty;

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// User who last changed the settings.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UpdatedBy"/>.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <summary>
    /// Audit records for this settings row.
    /// </summary>
    public ICollection<VereinSettingsAudit>? Audits { get; set; }
}
