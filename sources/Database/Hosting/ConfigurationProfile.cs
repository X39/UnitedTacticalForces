using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

[Index(nameof(GameIdentifier), nameof(IsArchived))]
public sealed class ConfigurationProfile : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(128)]
    public string GameIdentifier { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? CreatedBy { get; set; }

    public Guid CreatedByFk { get; set; }
    public bool IsArchived { get; set; }
    public Instant TimeStampCreated { get; set; }
    public Instant TimeStampUpdated { get; set; }
    public Instant? TimeStampArchived { get; set; }
    public ICollection<ConfigurationProfileRevision> Revisions { get; set; } = [];
    public ICollection<GameServer> GameServers { get; set; } = [];
}
