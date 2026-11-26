using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Cost alert configuration of one cost owner. It applies to every session billed to that owner.
/// </summary>
/// <remarks>
/// A cost owner without a setting row, or with an empty threshold list, never raises a cost alert.
/// </remarks>
[Index(nameof(CostOwnerFk), IsUnique = true)]
public sealed class CostAlertSetting : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the cost owner this setting belongs to.
    /// </summary>
    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the cost owner this setting belongs to.
    /// </summary>
    public Guid CostOwnerFk { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether alerts are delivered as Discord direct messages.
    /// </summary>
    public bool DiscordEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether alerts are delivered by email.
    /// </summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the revision counter that is incremented on every change, for optimistic concurrency of the
    /// settings editor.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets the user who changed the setting last.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the user who changed the setting last, or <see langword="null"/> when no user
    /// is recorded.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Gets or sets the instant the setting changed last.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// Gets or sets the absolute thresholds of this setting.
    /// </summary>
    public ICollection<CostAlertThreshold> Thresholds { get; set; } = [];
}
