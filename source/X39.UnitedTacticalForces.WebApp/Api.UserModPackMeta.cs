namespace X39.UnitedTacticalForces.WebApp;

public partial class UserModPackMeta
{
    public UserModPackMeta PartialCopy()
    {
        return new UserModPackMeta
        {
            User                = User?.ShallowCopy(),
            UserFk              = UserFk,
            ModPackDefinition   = ModPackDefinition?.ShallowCopy(),
            ModPackRevision     = ModPackRevision?.ShallowCopy(),
            TimeStampDownloaded = TimeStampDownloaded,
            ModPackDefinitionFk = ModPackDefinitionFk,
            ModPackRevisionFk   = ModPackRevisionFk,
        };
    }

    public UserModPackMeta ShallowCopy()
    {
        return new UserModPackMeta
        {
            User                = null,
            UserFk              = UserFk,
            ModPackDefinition   = null,
            ModPackRevision     = null,
            TimeStampDownloaded = TimeStampDownloaded,
            ModPackDefinitionFk = ModPackDefinitionFk,
            ModPackRevisionFk   = ModPackRevisionFk,
        };
    }
}