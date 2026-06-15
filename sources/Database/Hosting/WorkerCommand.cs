using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Persisted command delivered to one deployment-bound worker.
/// </summary>
[Index(nameof(DeploymentFk), nameof(Sequence), IsUnique = true)]
[Index(nameof(Status), nameof(Priority), nameof(TimeStampCreated))]
public class WorkerCommand : IPrimaryKey<Guid>, ITimeStampCreated
{
    public const string ActiveDeploymentIndexName = "IX_WorkerCommands_DeploymentFk_Active";
    public const string ActiveDeploymentIndexFilter = "\"Status\" IN (1, 2)";

    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(LifecycleJobFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleJob? LifecycleJob { get; set; }

    public Guid LifecycleJobFk { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid DeploymentFk { get; set; }

    [ForeignKey(nameof(WorkerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleWorker? Worker { get; set; }

    public Guid WorkerFk { get; set; }

    public long Sequence { get; set; }

    public int Priority { get; set; }

    [MaxLength(128)]
    public string ActionIdentifier { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";

    public EWorkerCommandStatus Status { get; set; }

    [MaxLength(128)]
    public string Stage { get; set; } = string.Empty;

    public int Attempt { get; set; }

    [MaxLength(256)]
    public string? LeaseOwner { get; set; }

    public Instant? LeaseExpires { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant? TimeStampReceived { get; set; }

    public Instant? TimeStampStarted { get; set; }

    public Instant? TimeStampCompleted { get; set; }

    [MaxLength(128)]
    public string? ErrorCode { get; set; }

    [MaxLength(2048)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Bounded, secret-redacted diagnostic output returned by a terminal worker command.
    /// </summary>
    [MaxLength(16384)]
    public string? DiagnosticOutput { get; set; }

    public ICollection<HostingAuditRecord> AuditRecords { get; set; } = [];
}
