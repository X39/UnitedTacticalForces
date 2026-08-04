using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Persisted installation-wide hosting affordability policy.
/// </summary>
public class HostingFinancePolicy : IPrimaryKey<int>
{
    public const int SingletonKey = 1;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int PrimaryKey { get; set; } = SingletonKey;

    [Column(TypeName = "numeric(18,4)")]
    public decimal MinimumSafetyRangeEur { get; set; }

    public int ReservationSliceMinutes { get; set; }

    public Instant TimeStampUpdated { get; set; }

    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    public Guid? UpdatedByFk { get; set; }
}
