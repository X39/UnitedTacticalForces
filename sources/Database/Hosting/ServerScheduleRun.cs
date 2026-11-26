using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// The durable record of what the control plane decided for one occurrence of a <see cref="ServerSchedule" />.
/// </summary>
/// <remarks>
/// The unique <c>(ScheduleFk, OccurrenceAt)</c> index is the exactly-once key of a schedule occurrence across API
/// restarts, repeated evaluation passes and concurrent instances: the evaluator inserts the run first and acts only
/// when the insert succeeds. <see cref="EServerScheduleRunOutcome.Failed" /> is derived at read time from the job,
/// so the evaluator never writes it. <see cref="EServerScheduleRunOutcome.WaitingForStop" /> is the one open outcome:
/// the runner later turns that row into <see cref="EServerScheduleRunOutcome.Started" /> or a skip under the server-row
/// lock, so the row stays the exactly-once key while a start waits for a stop of its server.
/// </remarks>
[Index(nameof(ScheduleFk), nameof(OccurrenceAt), IsUnique = true)]
[Index(nameof(GameServerFk), nameof(OccurrenceAt))]
public sealed class ServerScheduleRun : IPrimaryKey<Guid>
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the rule this occurrence belongs to. Deleting the rule deletes its runs.
    /// </summary>
    [ForeignKey(nameof(ScheduleFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ServerSchedule? Schedule { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the rule this occurrence belongs to.
    /// </summary>
    public Guid ScheduleFk { get; set; }

    /// <summary>
    /// Gets or sets the server the rule belongs to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server the rule belongs to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets the instant the occurrence was scheduled for.
    /// </summary>
    public Instant OccurrenceAt { get; set; }

    /// <summary>
    /// Gets or sets what the control plane decided for the occurrence.
    /// </summary>
    public EServerScheduleRunOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle job queued for the occurrence.
    /// </summary>
    [ForeignKey(nameof(LifecycleJobFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleJob? LifecycleJob { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the lifecycle job queued for the occurrence, or <see langword="null" /> when
    /// no job was queued.
    /// </summary>
    public Guid? LifecycleJobFk { get; set; }

    /// <summary>
    /// Gets or sets a short human-readable detail of the decision, or <see langword="null" /> when none applies.
    /// </summary>
    [MaxLength(256)]
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the instant the decision was recorded.
    /// </summary>
    public Instant RecordedAt { get; set; }
}
