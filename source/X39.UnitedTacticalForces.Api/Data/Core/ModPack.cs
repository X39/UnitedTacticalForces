using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Core;

public class ModPack
{
    [Key]
    public long PrimaryKey { get; set; }
    public DateTimeOffset TimeStampCreated { get; set; }
    public DateTimeOffset TimeStampUpdated { get; set; }
    public string Html { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [ForeignKey(nameof(OwnerFk))]
    public User? Owner { get; set; }
    public Guid OwnerFk { get; set; }
    public bool IsActive { get; set; }
    public ICollection<UserModPackMeta>? UserMetas { get; set; }
}