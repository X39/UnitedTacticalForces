using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

[Index(nameof(ConfigurationProfileFk), nameof(RevisionNumber), IsUnique = true)]
public sealed class ConfigurationProfileRevision : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(ConfigurationProfileFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ConfigurationProfile? ConfigurationProfile { get; set; }

    public Guid ConfigurationProfileFk { get; set; }
    public int RevisionNumber { get; set; }

    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    public Guid CreatedByFk { get; set; }
    public Instant TimeStampCreated { get; set; }
    public ICollection<ConfigurationProfileValue> Values { get; set; } = [];
    public ICollection<GameServer> PinnedGameServers { get; set; } = [];
}
