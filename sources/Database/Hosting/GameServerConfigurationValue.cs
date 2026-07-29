using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

[Index(nameof(GameServerFk), nameof(DefinitionKey), IsUnique = true)]
public sealed class GameServerConfigurationValue : IPrimaryKey<Guid>
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public GameServer? GameServer { get; set; }

    public Guid GameServerFk { get; set; }

    [MaxLength(256)]
    public string DefinitionKey { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string ValueJson { get; set; } = "null";

    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    public Guid UpdatedByFk { get; set; }
    public Instant TimeStampUpdated { get; set; }
}
