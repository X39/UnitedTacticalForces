using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// One automatic stop warning episode of a deployment: the limit that applied, when the owner was warned and when
/// the stop follows.
/// </summary>
/// <remarks>
/// The row is the single source of the in-app notice and of the <see cref="ECostAlertKind.AutoStop" /> cost alert.
/// A filtered unique index on the deployment for <see cref="EAutoStopStatus.Pending" /> rows allows one open warning
/// per deployment, so a restart, a repeated evaluation pass or a second instance can never open a second warning
/// episode.
/// </remarks>
public sealed class AutomationPendingStop : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <summary>
    /// The name of the filtered unique index that allows one pending stop per deployment.
    /// </summary>
    public const string PendingIndexName = "IX_AutomationPendingStops_DeploymentFk_Pending";

    /// <summary>
    /// The filter of the unique index that restricts it to <see cref="EAutoStopStatus.Pending" /> rows.
    /// </summary>
    public const string PendingIndexFilter = "\"Status\" = 0";

    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

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
    /// Gets or sets the deployment the warning applies to.
    /// </summary>
    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the deployment the warning applies to.
    /// </summary>
    public Guid DeploymentFk { get; set; }

    /// <summary>
    /// Gets or sets why the warning was opened.
    /// </summary>
    public EAutoStopReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the limit that applied, in minutes for an idle stop and in hours for a maximum runtime stop, or
    /// <see langword="null" /> for a schedule stop.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the schedule rule that caused the warning. The reference is cleared when the rule is deleted.
    /// </summary>
    [ForeignKey(nameof(ScheduleFk))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public ServerSchedule? Schedule { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the schedule rule that caused the warning, or <see langword="null" /> when
    /// the warning is not schedule-driven or the rule was deleted.
    /// </summary>
    public Guid? ScheduleFk { get; set; }

    /// <summary>
    /// Gets or sets the occurrence instant of the schedule rule that caused the warning, or <see langword="null" />
    /// when the warning is not schedule-driven.
    /// </summary>
    public Instant? ScheduleOccurrenceAt { get; set; }

    /// <summary>
    /// Gets or sets the instant the owner was warned, or <see langword="null" /> when the warning was not delivered
    /// yet.
    /// </summary>
    public Instant? WarnedAt { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the stop is requested unless the warning is cancelled first.
    /// </summary>
    public Instant StopsAt { get; set; }

    /// <summary>
    /// Gets or sets the state of the warning episode.
    /// </summary>
    public EAutoStopStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a short machine-readable reason a pending warning was cancelled or superseded, or
    /// <see langword="null" /> when it was not.
    /// </summary>
    [MaxLength(64)]
    public string? CancelReason { get; set; }

    /// <summary>
    /// Gets or sets the stop job queued when the warning elapsed.
    /// </summary>
    [ForeignKey(nameof(LifecycleJobFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleJob? LifecycleJob { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the stop job queued when the warning elapsed, or <see langword="null" /> when
    /// no job was queued.
    /// </summary>
    public Guid? LifecycleJobFk { get; set; }

    /// <summary>
    /// Gets or sets the instant the warning left the pending state, or <see langword="null" /> while it is pending.
    /// </summary>
    public Instant? ResolvedAt { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }
}
