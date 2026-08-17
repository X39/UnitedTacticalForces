using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Database.Core;

[PrimaryKey(nameof(ParentRevisionFk), nameof(ChildDefinitionFk))]
public sealed class ModPackRevisionDependency
{
    [ForeignKey(nameof(ParentRevisionFk)), DeleteBehavior(DeleteBehavior.Cascade)] public ModPackRevision? ParentRevision { get; set; }
    public long ParentRevisionFk { get; set; }
    [ForeignKey(nameof(ChildDefinitionFk)), DeleteBehavior(DeleteBehavior.Restrict)] public ModPackDefinition? ChildDefinition { get; set; }
    public long ChildDefinitionFk { get; set; }
    [ForeignKey(nameof(PinnedChildRevisionFk)), DeleteBehavior(DeleteBehavior.SetNull)] public ModPackRevision? PinnedChildRevision { get; set; }
    public long? PinnedChildRevisionFk { get; set; }
    public int Order { get; set; }
}
