using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Historical interval during which a cost owner was assigned to a server.
/// </summary>
[Index(nameof(GameServerFk), nameof(TimeStampAssigned))]
public class GameServerCostOwnerAssignment : IPrimaryKey<Guid>
{
    public const string ActiveServerIndexName = "IX_GameServerCostOwnerAssignments_GameServerFk_Active";
    public const string ActiveServerIndexFilter = "\"TimeStampUnassigned\" IS NULL";

    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    public Guid GameServerFk { get; set; }

    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; set; }

    public Guid CostOwnerFk { get; set; }

    [ForeignKey(nameof(AssignedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? AssignedBy { get; set; }

    public Guid AssignedByFk { get; set; }

    [ForeignKey(nameof(UnassignedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UnassignedBy { get; set; }

    public Guid? UnassignedByFk { get; set; }

    [MaxLength(1024)]
    public string? ChangeReason { get; set; }

    public Instant TimeStampAssigned { get; set; }

    public Instant? TimeStampUnassigned { get; set; }
}
