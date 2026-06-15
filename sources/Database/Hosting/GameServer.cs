using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Durable game-server identity retained independently of deployment lifetimes.
/// </summary>
[Index(nameof(GameIdentifier))]
[Index(nameof(IsArchived), nameof(IsLaunchDisabled))]
public class GameServer : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [MaxLength(1024)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(128)]
    public string GameIdentifier { get; set; } = string.Empty;

    [MaxLength(128)]
    public string RuntimeProviderIdentifier { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    public Guid CreatedByFk { get; set; }

    [ForeignKey(nameof(CurrentCostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CurrentCostOwner { get; set; }

    public Guid CurrentCostOwnerFk { get; set; }

    public bool IsLaunchDisabled { get; set; }

    [MaxLength(1024)]
    public string? LaunchDisabledReason { get; set; }

    public bool IsArchived { get; set; }

    public int GracefulStopAttempts { get; set; } = 3;

    public int GracefulStopTimeoutSeconds { get; set; } = 60;

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    public Instant? TimeStampArchived { get; set; }

    public ICollection<GameServerCostOwnerAssignment> CostOwnerAssignments { get; set; } = [];

    public ICollection<GameServerDeployment> Deployments { get; set; } = [];

    public ICollection<LifecycleJob> LifecycleJobs { get; set; } = [];

    public ICollection<PreservationSelection> PreservationSelections { get; set; } = [];

    public ICollection<HostingAuditRecord> AuditRecords { get; set; } = [];
}
