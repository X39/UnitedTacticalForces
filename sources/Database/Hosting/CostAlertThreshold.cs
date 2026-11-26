using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// One absolute accrued-cost amount of a <see cref="CostAlertSetting" /> that raises an alert when a session
/// reaches it.
/// </summary>
[Index(nameof(SettingFk), nameof(Amount), IsUnique = true)]
public sealed class CostAlertThreshold : IPrimaryKey<Guid>
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the setting that owns this threshold. Deleting the setting deletes its thresholds.
    /// </summary>
    [ForeignKey(nameof(SettingFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public CostAlertSetting? Setting { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the setting that owns this threshold.
    /// </summary>
    public Guid SettingFk { get; set; }

    /// <summary>
    /// Gets or sets the accrued amount, in the cost owner's currency, at which an alert is raised.
    /// </summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }
}
