using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// One runtime/provider-resource lifetime for a durable server.
/// </summary>
[Index(nameof(GameServerFk), nameof(TimeStampCreated))]
[Index(nameof(Status), nameof(TimeStampUpdated))]
public class GameServerDeployment : IPrimaryKey<Guid>, ITimeStampCreated
{
    public const string ActiveServerIndexName = "IX_GameServerDeployments_GameServerFk_Active";
    public const string ActiveServerIndexFilter = "\"TimeStampResolved\" IS NULL";

    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    public Guid GameServerFk { get; set; }

    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; init; }

    public Guid CostOwnerFk { get; init; }

    public EDeploymentStatus Status { get; set; }

    [MaxLength(128)]
    public string Stage { get; set; } = string.Empty;

    public bool IsProvisioningBlocked { get; set; }

    public bool PossibleCost { get; set; }

    [MaxLength(128)]
    public string? FaultCode { get; set; }

    [MaxLength(2048)]
    public string? FaultMessage { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    public Instant? TimeStampStarted { get; set; }

    public Instant? TimeStampFaulted { get; set; }

    public Instant? TimeStampResolved { get; set; }

    /// <summary>
    /// End of the provider-cost slice already charged to this deployment's cost owner.
    /// </summary>
    public Instant? CostAuthorizationExpiresAt { get; set; }

    /// <summary>
    /// Start of an affordability reservation made before the first provider mutation.
    /// The reservation reduces spendable headroom but is not a financial charge.
    /// </summary>
    public Instant? CostReservationStartsAt { get; set; }

    /// <summary>
    /// End of the initial provider-cost slice reserved before provider mutation.
    /// </summary>
    public Instant? CostReservationExpiresAt { get; set; }

    /// <summary>
    /// Amount reserved for the initial provider-cost slice. It is committed only once provider cost is possible.
    /// </summary>
    [Column(TypeName = "numeric(18,4)")]
    public decimal? ReservedProviderCostEur { get; set; }

    [Column(TypeName = "numeric(18,6)")]
    public decimal? EstimatedHourlyCostEur { get; set; }

    public ICollection<DeploymentResource> Resources { get; set; } = [];

    public ICollection<LifecycleWorker> Workers { get; set; } = [];

    public ICollection<LifecycleJob> LifecycleJobs { get; set; } = [];

    public ICollection<WorkerCommand> WorkerCommands { get; set; } = [];

    public ICollection<HostingAuditRecord> AuditRecords { get; set; } = [];

    public ICollection<HostingCostLedgerEntry> CostLedgerEntries { get; set; } = [];
}
