using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class ModPackDefinition
{
    public static ModPackDefinitionDto ToModPackDefinitionDto(this FullModPackDefinitionDto self) => new()
    {
        AdditionalData           = self.AdditionalData,
        IsActive                 = self.IsActive,
        IsComposition            = self.IsComposition,
        MetaTimeStampDownloaded  = null,
        OwnerFk                  = self.Owner?.PrimaryKey,
        PrimaryKey               = self.PrimaryKey,
        RevisionHtml             = self.ModPackRevisions?.FirstOrDefault()?.Html,
        RevisionIsActive         = self.ModPackRevisions?.FirstOrDefault()?.IsActive,
        RevisionPrimaryKey       = self.ModPackRevisions?.FirstOrDefault()?.PrimaryKey,
        RevisionTimeStampCreated = self.ModPackRevisions?.FirstOrDefault()?.TimeStampCreated,
        RevisionUpdatedByFk      = self.ModPackRevisions?.FirstOrDefault()?.UpdatedByFk,
        TimeStampCreated         = self.TimeStampCreated,
        Title                    = self.Title,
    };
    public static ModPackRevisionDto ToModPackRevisionDto(this FullModPackDefinitionDto self) => new()
    {
        AdditionalData             = self.AdditionalData,
        DefinitionIsActive         = self.IsActive,
        DefinitionIsComposition    = self.IsComposition,
        DefinitionOwnerFk          = self.Owner?.PrimaryKey,
        DefinitionPrimaryKey       = self.PrimaryKey,
        DefinitionTimeStampCreated = self.TimeStampCreated,
        DefinitionTitle            = self.Title,
        Html                       = self.ModPackRevisions?.FirstOrDefault()?.Html,
        IsActive                   = self.ModPackRevisions?.FirstOrDefault()?.IsActive,
        MetaTimeStampDownloaded    = null,
        PrimaryKey                 = self.ModPackRevisions?.FirstOrDefault()?.PrimaryKey,
        TimeStampCreated           = self.ModPackRevisions?.FirstOrDefault()?.TimeStampCreated,
        UpdatedByFk                = self.ModPackRevisions?.FirstOrDefault()?.UpdatedByFk,
    };
}
