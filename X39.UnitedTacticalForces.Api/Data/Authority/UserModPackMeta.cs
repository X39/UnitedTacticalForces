using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Core;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[PrimaryKey(nameof(UserFk), nameof(ModPackFk))]
public class UserModPackMeta
{
    [ForeignKey(nameof(UserFk))]
    public User? User { get; set; }
    public Guid UserFk { get; set; }
    [ForeignKey(nameof(ModPackFk))]
    public ModPack? ModPack { get; set; }
    public long ModPackFk { get; set; }
    
    public DateTimeOffset TimeStampDownloaded { get; set; }
}