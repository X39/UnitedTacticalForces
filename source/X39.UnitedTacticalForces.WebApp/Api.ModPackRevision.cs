namespace X39.UnitedTacticalForces.WebApp;

public partial class ModPackRevision
{
    public ModPackRevision PartialCopy()
    {
        return new ModPackRevision
        {
            IsActive         = IsActive,
            PrimaryKey       = PrimaryKey,
            TimeStampCreated = TimeStampCreated,
            Definition       = Definition?.ShallowCopy(),
            Html             = Html,
            DefinitionFk     = DefinitionFk,
            UpdatedBy        = UpdatedBy?.ShallowCopy(),
            UserMetas        = UserMetas?.Select((q) => q.PartialCopy()).ToList(),
            UpdatedByFk      = UpdatedByFk,
        };
    }

    public ModPackRevision ShallowCopy()
    {
        return new ModPackRevision
        {
            IsActive         = IsActive,
            PrimaryKey       = PrimaryKey,
            TimeStampCreated = TimeStampCreated,
            Definition       = null,
            Html             = Html,
            DefinitionFk     = DefinitionFk,
            UpdatedBy        = null,
            UserMetas        = new List<UserModPackMeta>(),
            UpdatedByFk      = UpdatedByFk,
        };
    }

    public void Apply(ModPackRevision other)
    {
        IsActive         = other.IsActive;
        PrimaryKey       = other.PrimaryKey;
        TimeStampCreated = other.TimeStampCreated;
        Definition       = other.Definition;
        Html             = other.Html;
        DefinitionFk     = other.DefinitionFk;
        UpdatedBy        = other.UpdatedBy;
        UserMetas        = other.UserMetas;
        UpdatedByFk      = other.UpdatedByFk;
    }
}