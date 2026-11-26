using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Records that one viewer dismissed one cost alert. Dismissal is per viewer, so it never hides the alert for other
/// members of the cost owner.
/// </summary>
[Index(nameof(CostAlertFk), nameof(UserFk), IsUnique = true)]
public sealed class CostAlertDismissal : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the dismissed alert.
    /// </summary>
    [ForeignKey(nameof(CostAlertFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostAlert? CostAlert { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the dismissed alert.
    /// </summary>
    public Guid CostAlertFk { get; set; }

    /// <summary>
    /// Gets or sets the viewer who dismissed the alert.
    /// </summary>
    [ForeignKey(nameof(UserFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the viewer who dismissed the alert.
    /// </summary>
    public Guid UserFk { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
