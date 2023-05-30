using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Core;

public class ModPackRevision
{
    [Key]
    public long PrimaryKey { get; set; }

    public DateTimeOffset TimeStampCreated { get; set; }
    public string Html { get; set; } = string.Empty;

    [ForeignKey(nameof(UpdatedByFk))]
    public User? UpdatedBy { get; set; }

    public Guid UpdatedByFk { get; set; }
    public bool IsActive { get; set; }
    public ICollection<UserModPackMeta>? UserMetas { get; set; }
    [ForeignKey(nameof(DefinitionFk))]
    public ModPackDefinition? Definition { get; set; }
    public long DefinitionFk { get; set; }
}