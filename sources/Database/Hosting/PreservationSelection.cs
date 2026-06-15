using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Server-relative live path selected for publication to persistent storage at lifecycle boundaries.
/// </summary>
[Index(nameof(GameServerFk), nameof(Path), nameof(Scope), IsUnique = true)]
public class PreservationSelection : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    public Guid GameServerFk { get; set; }

    [MaxLength(2048)]
    public string Path { get; set; } = string.Empty;

    public EPreservationScope Scope { get; set; }

    public EPreservationMode Mode { get; set; }

    public bool IsGameSeeded { get; set; }

    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    public Guid? CreatedByFk { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }
}
