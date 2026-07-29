using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

[Index(nameof(ConfigurationProfileRevisionFk), nameof(DefinitionKey), IsUnique = true)]
public sealed class ConfigurationProfileValue : IPrimaryKey<Guid>
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(ConfigurationProfileRevisionFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ConfigurationProfileRevision? ConfigurationProfileRevision { get; set; }

    public Guid ConfigurationProfileRevisionFk { get; set; }

    [MaxLength(256)]
    public string DefinitionKey { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string ValueJson { get; set; } = "null";
}
