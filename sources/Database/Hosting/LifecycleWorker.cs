using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Persistent identity and health state of a deployment's dedicated worker.
/// </summary>
[Index(nameof(DeploymentFk), nameof(Status))]
[Index(nameof(BootstrapCredentialExpiresAt))]
[Index(nameof(WorkerCredentialExpiresAt))]
[Index(nameof(PendingWorkerCredentialExpiresAt))]
public class LifecycleWorker : IPrimaryKey<Guid>, ITimeStampCreated
{
    public const string ActiveDeploymentIndexName = "IX_LifecycleWorkers_DeploymentFk_Active";
    public const string ActiveDeploymentIndexFilter = "\"TimeStampStopped\" IS NULL";

    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid DeploymentFk { get; set; }

    [MaxLength(256)]
    public string Identity { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the short-lived bootstrap credential. Registration may retry it after a
    /// lost response, but it is retired when an issued worker credential first authenticates.
    /// The plaintext credential is returned only to the provisioning caller.
    /// </summary>
    [MaxLength(64)]
    public string? BootstrapCredentialHash { get; set; }

    public Instant? BootstrapCredentialExpiresAt { get; set; }

    public Instant? BootstrapCredentialConsumedAt { get; set; }

    /// <summary>
    /// SHA-256 hash of the deployment-scoped worker credential.
    /// </summary>
    [MaxLength(64)]
    public string? WorkerCredentialHash { get; set; }

    public Instant? WorkerCredentialIssuedAt { get; set; }

    public Instant? WorkerCredentialExpiresAt { get; set; }

    /// <summary>
    /// Time at which the current deployment-scoped credential first proved receipt by authenticating.
    /// </summary>
    public Instant? WorkerCredentialActivatedAt { get; set; }

    public Instant? WorkerCredentialRevokedAt { get; set; }

    /// <summary>
    /// SHA-256 hash of an issued replacement credential. The current credential remains valid until
    /// this pending credential authenticates and is promoted atomically.
    /// </summary>
    [MaxLength(64)]
    public string? PendingWorkerCredentialHash { get; set; }

    public Instant? PendingWorkerCredentialIssuedAt { get; set; }

    public Instant? PendingWorkerCredentialExpiresAt { get; set; }

    public EWorkerStatus Status { get; set; }

    public Instant? LastHeartbeatAt { get; set; }

    [MaxLength(256)]
    public string? LeaseOwner { get; set; }

    public Instant? LeaseExpires { get; set; }

    public int? ProcessId { get; set; }

    public Instant? ProcessStartedAt { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    public Instant? TimeStampRegistered { get; set; }

    public Instant? TimeStampStopped { get; set; }

    public ICollection<LifecycleJobAttempt> JobAttempts { get; set; } = [];

    public ICollection<WorkerCommand> Commands { get; set; } = [];

    public ICollection<HostingAuditRecord> AuditRecords { get; set; } = [];
}
