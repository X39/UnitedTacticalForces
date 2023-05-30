using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class ModPackDefinition
{
    public ModPackDefinition DeepCopy()
    {
        return new ModPackDefinition
        {
            Title            = Title,
            IsActive         = IsActive,
            PrimaryKey       = PrimaryKey,
            TimeStampCreated = TimeStampCreated,
            Owner            = Owner?.PartialCopy(),
            OwnerFk          = OwnerFk,
            ModPackRevisions = ModPackRevisions?.NotNull().Select((q) => q.PartialCopy()).ToList(),
        };
    }

    public ModPackDefinition ShallowCopy()
    {
        return new ModPackDefinition
        {
            Title            = Title,
            IsActive         = IsActive,
            PrimaryKey       = PrimaryKey,
            TimeStampCreated = TimeStampCreated,
            OwnerFk          = OwnerFk,
            Owner            = null,
            ModPackRevisions = new List<ModPackRevision>(),
        };
    }

    public void Apply(ModPackDefinition modifiedModPack)
    {
        ModPackRevisions ??= new List<ModPackRevision>();
        foreach (var modPackRevision in modifiedModPack.ModPackRevisions ?? Enumerable.Empty<ModPackRevision>())
        {
            var existingRevision = ModPackRevisions.FirstOrDefault((q) => q.PrimaryKey == modPackRevision.PrimaryKey);
            if (existingRevision is null)
                ModPackRevisions.Add(modPackRevision);
            else
                existingRevision.Apply(modPackRevision);
        }
        OwnerFk          = modifiedModPack.OwnerFk;
        Owner            = modifiedModPack.Owner;
        TimeStampCreated = modifiedModPack.TimeStampCreated;
        PrimaryKey       = modifiedModPack.PrimaryKey;
        IsActive         = modifiedModPack.IsActive;
        Title            = modifiedModPack.Title;
    }

    public ModPackRevision? GetActiveRevision() => ModPackRevisions?.FirstOrDefault((q) => q.IsActive ?? false);
}