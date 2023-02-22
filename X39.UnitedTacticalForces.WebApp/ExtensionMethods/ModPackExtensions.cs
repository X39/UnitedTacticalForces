namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class ModPackExtensions
{
    public static ModPack Clone(this ModPack self) => new()
    {
        PrimaryKey = self.PrimaryKey,
        Html = self.Html,
        Owner = self.Owner,
        Title = self.Title,
        IsActive = self.IsActive,
        OwnerFk = self.OwnerFk,
        UserMetas = self.UserMetas,
        TimeStampCreated = self.TimeStampCreated,
        TimeStampUpdated = self.TimeStampUpdated,
    };
    public static void Apply(this ModPack self, ModPack other)
    {
        self.PrimaryKey = other.PrimaryKey;
        self.Html = other.Html;
        self.Owner = other.Owner;
        self.Title = other.Title;
        self.IsActive = other.IsActive;
        self.OwnerFk = other.OwnerFk;
        self.UserMetas = other.UserMetas;
        self.TimeStampCreated = other.TimeStampCreated;
        self.TimeStampUpdated = other.TimeStampUpdated;
    }
}