using System.Text;

namespace X39.UnitedTacticalForces.WebApp;

public partial class Terrain
{
    public string ToImageSource()
    {
        if (@Image is null)
            throw new ArgumentException("Event.Image is null");
        var builder = new StringBuilder();
        builder.Append("data:");
        builder.Append(@ImageMimeType);
        builder.Append("; base64, ");
        builder.Append(Convert.ToBase64String(@Image));
        return builder.ToString();
    }
    public Terrain DeepCopy()
    {
        return new Terrain
        {
            PrimaryKey = PrimaryKey,
            Image = Image,
            Title = Title,
            ImageMimeType = ImageMimeType,
            IsActive = IsActive,
        };
    }

    public Terrain ShallowCopy()
    {
        return new Terrain
        {
            PrimaryKey    = PrimaryKey,
            Image         = Image,
            Title         = Title,
            ImageMimeType = ImageMimeType,
            IsActive      = IsActive,
        };
    }
    public void Apply(Terrain other)
    {
        PrimaryKey    = other.PrimaryKey;
        Title         = other.Title;
        IsActive      = other.IsActive;
        Image         = other.Image;
        ImageMimeType = other.ImageMimeType;
    }
}