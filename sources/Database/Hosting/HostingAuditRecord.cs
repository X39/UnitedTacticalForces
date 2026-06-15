using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Append-only, sanitized audit record for hosting operations.
/// </summary>
[Index(nameof(GameServerFk), nameof(TimeStampCreated), AllDescending = true)]
[Index(nameof(Kind), nameof(TimeStampCreated), AllDescending = true)]
public class HostingAuditRecord : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    public EHostingAuditEventKind Kind { get; set; }

    [ForeignKey(nameof(ActorFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? Actor { get; set; }

    public Guid? ActorFk { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    public Guid? GameServerFk { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid? DeploymentFk { get; set; }

    [ForeignKey(nameof(LifecycleJobFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleJob? LifecycleJob { get; set; }

    public Guid? LifecycleJobFk { get; set; }

    [ForeignKey(nameof(WorkerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public LifecycleWorker? Worker { get; set; }

    public Guid? WorkerFk { get; set; }

    [ForeignKey(nameof(WorkerCommandFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public WorkerCommand? WorkerCommand { get; set; }

    public Guid? WorkerCommandFk { get; set; }

    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; set; }

    public Guid? CostOwnerFk { get; set; }

    public ECostImpact CostImpact { get; set; }

    [MaxLength(512)]
    public string Summary { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string Details { get; set; } = "{}";

    public Instant TimeStampCreated { get; set; }
}
