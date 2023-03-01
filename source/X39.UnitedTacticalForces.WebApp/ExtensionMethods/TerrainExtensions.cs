using System.Text;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class TerrainExtensions
{
    public static string ToImageSource(this Terrain @event)
    {
        if (@event.Image is null)
            throw new ArgumentException("Event.Image is null");
        var builder = new StringBuilder();
        builder.Append("data:");
        builder.Append(@event.ImageMimeType);
        builder.Append("; base64, ");
        builder.Append(Convert.ToBase64String(@event.Image));
        return builder.ToString();
    }
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