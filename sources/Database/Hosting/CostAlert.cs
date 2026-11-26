using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// The durable record of one alert about a deployment: either that its accrued cost reached one threshold of its
/// cost owner, or that an automatic stop warning was opened for it.
/// </summary>
/// <remarks>
/// For <see cref="ECostAlertKind.CostThreshold" /> alerts a filtered unique index on the deployment and threshold
/// amount (applied to kind 0 only) makes the crossing once-only across API restarts, duplicate evaluation passes and
/// concurrent instances. Rows are never deleted when a threshold is removed, so re-adding a threshold cannot alert
/// the same session twice. <see cref="ECostAlertKind.AutoStop" /> alerts carry <see cref="ThresholdAmount" /> = 0 and
/// <see cref="AccruedAmount" /> = 0 so the amount columns stay non-nullable, and a unique index on
/// <see cref="PendingStopFk" /> makes them once-only per pending stop.
/// </remarks>
[Index(nameof(CostOwnerFk), nameof(TimeStampCreated))]
public sealed class CostAlert : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <summary>
    /// The name of the filtered unique index that allows one threshold alert per deployment and threshold amount.
    /// </summary>
    public const string ThresholdIndexName = "IX_CostAlerts_DeploymentFk_ThresholdAmount";

    /// <summary>
    /// The filter of the unique index that restricts it to <see cref="ECostAlertKind.CostThreshold" /> rows.
    /// </summary>
    public const string ThresholdIndexFilter = "\"Kind\" = 0";

    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the deployment (session) whose cost reached the threshold.
    /// </summary>
    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the deployment whose cost reached the threshold.
    /// </summary>
    public Guid DeploymentFk { get; set; }

    /// <summary>
    /// Gets or sets the server the deployment belongs to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server the deployment belongs to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets the cost owner the session was billed to when the threshold was reached.
    /// </summary>
    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the cost owner the session was billed to.
    /// </summary>
    public Guid CostOwnerFk { get; set; }

    /// <summary>
    /// Gets or sets what this alert announces. Rows that existed before automation was added read as
    /// <see cref="ECostAlertKind.CostThreshold" />.
    /// </summary>
    public ECostAlertKind Kind { get; set; } = ECostAlertKind.CostThreshold;

    /// <summary>
    /// Gets or sets the automatic stop warning this alert announces. It is <see langword="null" /> for cost
    /// threshold alerts.
    /// </summary>
    [ForeignKey(nameof(PendingStopFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AutomationPendingStop? PendingStop { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the automatic stop warning this alert announces, or <see langword="null" />
    /// for cost threshold alerts.
    /// </summary>
    public Guid? PendingStopFk { get; set; }

    /// <summary>
    /// Gets or sets the threshold amount that was reached, copied from the setting so later threshold edits do not
    /// change history. It is 0 for automatic stop alerts.
    /// </summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal ThresholdAmount { get; set; }

    /// <summary>
    /// Gets or sets the accrued cost at the instant the crossing was recorded.
    /// </summary>
    [Column(TypeName = "numeric(18,4)")]
    public decimal AccruedAmount { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code of the amounts.
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Gets or sets the delivery intents recorded for this alert, one per channel and recipient.
    /// </summary>
    public ICollection<CostAlertDelivery> Deliveries { get; set; } = [];

    /// <summary>
    /// Gets or sets the per-viewer dismissals of this alert.
    /// </summary>
    public ICollection<CostAlertDismissal> Dismissals { get; set; } = [];
}
