using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Party to which a server deployment's provider cost is attributed.
/// </summary>
[Index(nameof(Kind))]
public class CostOwner : IPrimaryKey<Guid>, ITimeStampCreated
{
    public const string UniqueUserIndexName = "IX_CostOwners_UserFk_Unique";
    public const string UniqueUserIndexFilter = "\"UserFk\" IS NOT NULL";
    public const string UniqueVereinIndexName = "IX_CostOwners_Kind_Verein";
    public const string UniqueVereinIndexFilter = "\"Kind\" = 1";

    [Key]
    public Guid PrimaryKey { get; set; }

    public ECostOwnerKind Kind { get; set; }

    [ForeignKey(nameof(UserFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? User { get; set; }

    public Guid? UserFk { get; set; }

    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsFrozen { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal Balance { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal? Allowance { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    public ICollection<GameServer> CurrentServers { get; set; } = [];

    public ICollection<GameServerCostOwnerAssignment> ServerAssignments { get; set; } = [];

    public ICollection<GameServerDeployment> Deployments { get; set; } = [];

    public ICollection<HostingAuditRecord> AuditRecords { get; set; } = [];

    public ICollection<HostingCostLedgerEntry> HostingCostLedgerEntries { get; set; } = [];
}
