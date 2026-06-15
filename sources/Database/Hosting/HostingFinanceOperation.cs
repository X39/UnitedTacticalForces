using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Caller-identified append-only finance operation that groups balanced correction or reversal entries.
/// </summary>
[Index(nameof(DeploymentFk), nameof(Kind))]
public sealed class HostingFinanceOperation : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <summary>
    /// Stable caller-supplied operation identifier and retry key.
    /// </summary>
    [Key]
    public Guid PrimaryKey { get; set; }

    public EHostingFinanceOperationKind Kind { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid? DeploymentFk { get; set; }

    [ForeignKey(nameof(SourceCostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? SourceCostOwner { get; set; }

    public Guid? SourceCostOwnerFk { get; set; }

    [ForeignKey(nameof(DestinationCostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? DestinationCostOwner { get; set; }

    public Guid? DestinationCostOwnerFk { get; set; }

    [ForeignKey(nameof(TargetLedgerEntryFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public HostingCostLedgerEntry? TargetLedgerEntry { get; set; }

    public Guid? TargetLedgerEntryFk { get; set; }

    [ForeignKey(nameof(TargetFinanceOperationFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public HostingFinanceOperation? TargetFinanceOperation { get; set; }

    public Guid? TargetFinanceOperationFk { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    public Instant TimeStampEffective { get; set; }

    [ForeignKey(nameof(RecordedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? RecordedBy { get; set; }

    public Guid RecordedByFk { get; set; }

    [MaxLength(512)]
    public string Reason { get; set; } = string.Empty;

    public Instant TimeStampCreated { get; set; }

    public ICollection<HostingCostLedgerEntry> Entries { get; set; } = [];

    public ICollection<HostingFinanceCorrectionAllocation> AffectedLedgerEntries { get; set; } = [];

    public ICollection<HostingFinanceOperation> ReversalOperations { get; set; } = [];
}
