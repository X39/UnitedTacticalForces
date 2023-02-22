namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class TerrainExtensions
{
    public static Terrain Clone(this Terrain self) => new()
    {
        PrimaryKey = self.PrimaryKey,
        Title = self.Title,
        IsActive = self.IsActive,
        Image = self.Image,
        ImageMimeType = self.ImageMimeType,
    };
    public static void Apply(this Terrain self, Terrain other)
    {
        self.PrimaryKey = other.PrimaryKey;
        self.Title = other.Title;
        self.IsActive = other.IsActive;
        self.Image = other.Image;
        self.ImageMimeType = other.ImageMimeType;
    }
}