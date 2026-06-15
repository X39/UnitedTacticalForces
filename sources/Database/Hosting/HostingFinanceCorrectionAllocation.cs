using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Immutable allocation of a completed-deployment owner correction to one original provider-cost entry.
/// </summary>
[Index(nameof(FinanceOperationFk), nameof(AffectedLedgerEntryFk), IsUnique = true)]
public sealed class HostingFinanceCorrectionAllocation : IPrimaryKey<Guid>
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(FinanceOperationFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public HostingFinanceOperation? FinanceOperation { get; set; }

    public Guid FinanceOperationFk { get; set; }

    [ForeignKey(nameof(AffectedLedgerEntryFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public HostingCostLedgerEntry? AffectedLedgerEntry { get; set; }

    public Guid AffectedLedgerEntryFk { get; set; }

    /// <summary>
    /// Positive portion of the original provider-cost debit transferred by the correction.
    /// </summary>
    [Column(TypeName = "numeric(18,4)")]
    public decimal Amount { get; set; }
}
