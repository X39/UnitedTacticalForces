using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Durable lifecycle request processed independently of its originating HTTP request.
/// </summary>
[Index(nameof(GameServerFk), nameof(Sequence), IsUnique = true)]
[Index(nameof(Status), nameof(Priority), nameof(TimeStampCreated))]
public class LifecycleJob : IPrimaryKey<Guid>, ITimeStampCreated
{
    public const string ActiveDeploymentIndexName = "IX_LifecycleJobs_DeploymentFk_Active";
    public const string ActiveDeploymentIndexFilter = "\"Status\" IN (1, 2, 3)";

    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    public Guid GameServerFk { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid DeploymentFk { get; set; }

    [ForeignKey(nameof(RequestedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? RequestedBy { get; set; }

    /// <summary>
    /// User who requested the operation, or <see langword="null"/> for a control-plane initiated safety action.
    /// </summary>
    public Guid? RequestedByFk { get; set; }

    public ELifecycleJobKind Kind { get; set; }

    [MaxLength(128)]
    public string ActionIdentifier { get; set; } = string.Empty;

    public long Sequence { get; set; }

    public int Priority { get; set; }

    public ELifecycleJobStatus Status { get; set; }

    [MaxLength(128)]
    public string Stage { get; set; } = string.Empty;

    public int Progress { get; set; }

    public int Attempt { get; set; }

    public int MaxAttempts { get; set; } = 3;

    [MaxLength(256)]
    public string? LeaseOwner { get; set; }

    public Instant? LeaseExpires { get; set; }

    public Instant? TimeStampNextAttempt { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    public Instant? TimeStampStarted { get; set; }

    public Instant? TimeStampCompleted { get; set; }

    [MaxLength(128)]
    public string? FailureCode { get; set; }

    [MaxLength(2048)]
    public string? FailureMessage { get; set; }

    public ICollection<LifecycleJobAttempt> Attempts { get; set; } = [];

    public ICollection<WorkerCommand> WorkerCommands { get; set; } = [];

    public ICollection<HostingAuditRecord> AuditRecords { get; set; } = [];
}
