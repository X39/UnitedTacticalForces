using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Database.Core;

[PrimaryKey(nameof(ScenarioVersionFk), nameof(ModPackDefinitionFk))]
public sealed class ScenarioVersionRequiredModPack
{
    [ForeignKey(nameof(ScenarioVersionFk)), DeleteBehavior(DeleteBehavior.Cascade)] public ScenarioVersion? ScenarioVersion { get; set; }
    public Guid ScenarioVersionFk { get; set; }
    [ForeignKey(nameof(ModPackDefinitionFk)), DeleteBehavior(DeleteBehavior.Restrict)] public ModPackDefinition? ModPackDefinition { get; set; }
    public long ModPackDefinitionFk { get; set; }
    [ForeignKey(nameof(PinnedRevisionFk)), DeleteBehavior(DeleteBehavior.SetNull)] public ModPackRevision? PinnedRevision { get; set; }
    public long? PinnedRevisionFk { get; set; }
}
