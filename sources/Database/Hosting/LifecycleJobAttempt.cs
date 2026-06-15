using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Immutable diagnostic record for one lifecycle job execution attempt.
/// </summary>
[Index(nameof(LifecycleJobFk), nameof(AttemptNumber), IsUnique = true)]
public class LifecycleJobAttempt : IPrimaryKey<Guid>
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(LifecycleJobFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleJob? LifecycleJob { get; set; }

    public Guid LifecycleJobFk { get; set; }

    [ForeignKey(nameof(WorkerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleWorker? Worker { get; set; }

    public Guid? WorkerFk { get; set; }

    public int AttemptNumber { get; set; }

    public ELifecycleJobStatus Status { get; set; }

    [MaxLength(128)]
    public string Stage { get; set; } = string.Empty;

    public Instant TimeStampStarted { get; set; }

    public Instant? TimeStampCompleted { get; set; }

    [MaxLength(128)]
    public string? FailureCode { get; set; }

    [MaxLength(2048)]
    public string? FailureMessage { get; set; }
}
