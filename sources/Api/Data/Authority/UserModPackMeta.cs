using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Core;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[PrimaryKey(nameof(UserFk), nameof(ModPackDefinitionFk), nameof(ModPackRevisionFk))]
public class UserModPackMeta
{
    [ForeignKey(nameof(UserFk))]
    public User? User { get; set; }

    public Guid UserFk { get; set; }

    [ForeignKey(nameof(ModPackDefinitionFk))]
    public ModPackDefinition? ModPackDefinition { get; set; }

    public long ModPackDefinitionFk { get; set; }

    public DateTimeOffset TimeStampDownloaded { get; set; }

    [ForeignKey(nameof(ModPackRevisionFk))]
    public ModPackRevision? ModPackRevision { get; set; }

    public long ModPackRevisionFk { get; set; }
}