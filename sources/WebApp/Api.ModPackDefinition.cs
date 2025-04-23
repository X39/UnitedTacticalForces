using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class ModPackDefinition
{
    public ModPackDefinition DeepCopy()
    {
        return new ModPackDefinition
        {
            Title                 = Title,
            IsActive              = IsActive,
            PrimaryKey            = PrimaryKey,
            TimeStampCreated      = TimeStampCreated,
            Owner                 = Owner?.PartialCopy(),
            OwnerFk               = OwnerFk,
            ModPackRevisions      = ModPackRevisions?.NotNull().Select((q) => q.PartialCopy()).ToList(),
            ModPackRevisionsOwned = ModPackRevisionsOwned?.NotNull().Select((q) => q.PartialCopy()).ToList(),
            IsComposition         = IsComposition,
        };
    }

    public ModPackDefinition ShallowCopy()
    {
        return new ModPackDefinition
        {
            Title                 = Title,
            IsActive              = IsActive,
            PrimaryKey            = PrimaryKey,
            TimeStampCreated      = TimeStampCreated,
            OwnerFk               = OwnerFk,
            Owner                 = null,
            ModPackRevisions      = new List<ModPackRevision>(),
            ModPackRevisionsOwned = new List<ModPackRevision>(),
            IsComposition         = IsComposition,
        };
    }

    public void Apply(ModPackDefinition modifiedModPack)
    {
        ModPackRevisions ??= new List<ModPackRevision>();
        var tmpModPackRevisions = ModPackRevisions;
        ModPackRevisions.Clear();
        foreach (var modPackRevision in modifiedModPack.ModPackRevisions ?? Enumerable.Empty<ModPackRevision>())
        {
            var existingRevision = tmpModPackRevisions.FirstOrDefault((q) => q.PrimaryKey == modPackRevision.PrimaryKey);
            if (existingRevision is null)
                ModPackRevisions.Add(modPackRevision);
            else
            {
                existingRevision.Apply(modPackRevision);
                ModPackRevisions.Add(existingRevision);
            }
        }
        ModPackRevisionsOwned ??= new List<ModPackRevision>();
        var tmpModPackRevisionsOwned = ModPackRevisionsOwned;
        ModPackRevisionsOwned.Clear();
        foreach (var modPackRevision in modifiedModPack.ModPackRevisionsOwned ?? Enumerable.Empty<ModPackRevision>())
        {
            var existingRevision = tmpModPackRevisionsOwned.FirstOrDefault((q) => q.PrimaryKey == modPackRevision.PrimaryKey);
            if (existingRevision is null)
                ModPackRevisionsOwned.Add(modPackRevision);
            else
            {
                existingRevision.Apply(modPackRevision);
                ModPackRevisionsOwned.Add(existingRevision);
            }
        }
        OwnerFk          = modifiedModPack.OwnerFk;
        Owner            = modifiedModPack.Owner;
        TimeStampCreated = modifiedModPack.TimeStampCreated;
        PrimaryKey       = modifiedModPack.PrimaryKey;
        IsActive         = modifiedModPack.IsActive;
        Title            = modifiedModPack.Title;
        IsComposition    = modifiedModPack.IsComposition;
    }

    public ModPackRevision? GetActiveRevision() => ModPackRevisionsOwned?.FirstOrDefault((q) => q.IsActive ?? false) ?? ModPackRevisions?.FirstOrDefault((q) => q.IsActive ?? false);
}