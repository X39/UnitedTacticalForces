using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Append-only hosting-finance entry. Balance changes and allowance policy changes share one ordered audit stream.
/// </summary>
[Index(nameof(CostOwnerFk), nameof(TimeStampCreated))]
[Index(nameof(DeploymentFk), nameof(SliceStartsAt), IsUnique = true)]
public sealed class HostingCostLedgerEntry : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; set; }

    public Guid CostOwnerFk { get; set; }

    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }

    public Guid? DeploymentFk { get; set; }

    public EHostingLedgerEntryKind Kind { get; set; }

    /// <summary>
    /// Signed balance delta. Provider cost consumption is negative.
    /// </summary>
    [Column(TypeName = "numeric(18,4)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal BalanceAfter { get; set; }

    [Column(TypeName = "numeric(18,6)")]
    public decimal? EstimatedHourlyRate { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [MaxLength(128)]
    public string? ProviderIdentifier { get; set; }

    public Instant? SliceStartsAt { get; set; }

    public Instant? SliceEndsAt { get; set; }

    /// <summary>
    /// Stable caller-supplied identifier for retry-safe manual finance mutations.
    /// </summary>
    public Guid? RecordIdentifier { get; set; }

    [ForeignKey(nameof(FinanceOperationFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public HostingFinanceOperation? FinanceOperation { get; set; }

    /// <summary>
    /// Stable multi-entry correction or reversal operation that owns this entry.
    /// </summary>
    public Guid? FinanceOperationFk { get; set; }

    [ForeignKey(nameof(ReversedEntryFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public HostingCostLedgerEntry? ReversedEntry { get; set; }

    /// <summary>
    /// Original append-only entry negated by this reversal entry.
    /// </summary>
    public Guid? ReversedEntryFk { get; set; }

    /// <summary>
    /// Effective booking time supplied for a manual finance mutation.
    /// </summary>
    public Instant TimeStampEffective { get; set; }

    [ForeignKey(nameof(RecordedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? RecordedBy { get; set; }

    public Guid? RecordedByFk { get; set; }

    [MaxLength(128)]
    public string? ExternalReference { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal? PreviousAllowance { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal? NewAllowance { get; set; }

    public Instant TimeStampCreated { get; set; }

    public ICollection<HostingCostLedgerEntry> ReversalEntries { get; set; } = [];

    public ICollection<HostingFinanceCorrectionAllocation> CorrectionAllocations { get; set; } = [];
}
