using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// The persisted delivery intent of one cost alert for one channel and one recipient, used as an outbox row with
/// lease and retry bookkeeping.
/// </summary>
/// <remarks>
/// Rows are inserted in the same transaction as their <see cref="CostAlert" /> before any side effect happens. A
/// row that cannot be delivered is stored as <see cref="ECostAlertDeliveryStatus.Skipped" /> with a reason code, so
/// the in-app alert exists even when no channel can reach anybody.
/// </remarks>
[Index(nameof(CostAlertFk), nameof(Channel), nameof(RecipientUserFk), IsUnique = true)]
[Index(nameof(Status), nameof(TimeStampNextAttempt))]
public sealed class CostAlertDelivery : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the alert that is delivered.
    /// </summary>
    [ForeignKey(nameof(CostAlertFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostAlert? CostAlert { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the alert that is delivered.
    /// </summary>
    public Guid CostAlertFk { get; set; }

    /// <summary>
    /// Gets or sets the channel the alert is delivered through.
    /// </summary>
    public ECostAlertChannel Channel { get; set; }

    /// <summary>
    /// Gets or sets the recipient of the delivery.
    /// </summary>
    [ForeignKey(nameof(RecipientUserFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? RecipientUser { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the recipient, or <see langword="null"/> when nobody resolved for the alert.
    /// </summary>
    public Guid? RecipientUserFk { get; set; }

    /// <summary>
    /// Gets or sets the delivery state.
    /// </summary>
    public ECostAlertDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of delivery attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the identity of the dispatcher instance that holds the lease while the status is
    /// <see cref="ECostAlertDeliveryStatus.Sending" />.
    /// </summary>
    [MaxLength(256)]
    public string? LeaseOwner { get; set; }

    /// <summary>
    /// Gets or sets the instant the current lease expires.
    /// </summary>
    public Instant? LeaseExpires { get; set; }

    /// <summary>
    /// Gets or sets the earliest instant the next attempt may be made.
    /// </summary>
    public Instant? TimeStampNextAttempt { get; set; }

    /// <summary>
    /// Gets or sets the fixed reason code explaining a skipped or failed delivery.
    /// </summary>
    [MaxLength(128)]
    public string? StatusReason { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Gets or sets the instant the row changed last.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// Gets or sets the instant the channel accepted the message.
    /// </summary>
    public Instant? TimeStampSent { get; set; }
}
