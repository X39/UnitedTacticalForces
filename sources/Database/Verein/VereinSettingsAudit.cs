using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Audit entry for public Verein settings changes.
/// </summary>
[Index(nameof(SettingsFk), nameof(TimeStampCreated), AllDescending = true)]
public class VereinSettingsAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Settings row this audit entry belongs to.
    /// </summary>
    [ForeignKey(nameof(SettingsFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public VereinSettings? Settings { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Settings"/>.
    /// </summary>
    public long SettingsFk { get; set; }

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
    /// Action performed on the settings row.
    /// </summary>
    public EVereinSettingsAuditAction Action { get; set; }

    /// <summary>
    /// JSON object containing concise non-secret change metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
