using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

[Index(nameof(GameServerFk), nameof(Key), nameof(Version), IsUnique = true)]
public sealed class GameServerSecret
{
    [Key] public Guid PrimaryKey { get; set; }
    [ForeignKey(nameof(GameServerFk)), DeleteBehavior(DeleteBehavior.Cascade)] public GameServer? GameServer { get; set; }
    public Guid GameServerFk { get; set; }
    [MaxLength(128)] public string Key { get; set; } = string.Empty;
    public int Version { get; set; }
    public EGameServerSecretOrigin Origin { get; set; }
    public bool IsCurrent { get; set; }
    public string ProtectedValue { get; set; } = string.Empty;
    public Instant TimeStampCreated { get; set; }
    [ForeignKey(nameof(CreatedByFk)), DeleteBehavior(DeleteBehavior.Restrict)] public User? CreatedBy { get; set; }
    public Guid? CreatedByFk { get; set; }
    public Instant? TimeStampRetired { get; set; }
}
