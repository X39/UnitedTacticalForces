using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Database.Core;

[PrimaryKey(nameof(ModPackRevisionFk), nameof(ModFk))]
public sealed class ModPackRevisionMod
{
    [ForeignKey(nameof(ModPackRevisionFk)), DeleteBehavior(DeleteBehavior.Cascade)] public ModPackRevision? ModPackRevision { get; set; }
    public long ModPackRevisionFk { get; set; }
    [ForeignKey(nameof(ModFk)), DeleteBehavior(DeleteBehavior.Restrict)] public Mod? Mod { get; set; }
    public Guid ModFk { get; set; }
    [ForeignKey(nameof(PinnedVersionFk)), DeleteBehavior(DeleteBehavior.SetNull)] public ModVersion? PinnedVersion { get; set; }
    public Guid? PinnedVersionFk { get; set; }
    public int Order { get; set; }
}
