using System.Text;

namespace X39.UnitedTacticalForces.WebApp;

public partial class Event
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

    public Event Clone() => new()
    {
        PrimaryKey           = PrimaryKey,
        Owner                = Owner,
        Title                = Title,
        OwnerFk              = OwnerFk,
        UserMetas            = UserMetas,
        TimeStampCreated     = TimeStampCreated,
        ModPack              = ModPack,
        Terrain              = Terrain,
        Description          = Description,
        Image                = Image,
        AcceptedCount        = AcceptedCount,
        HostedBy             = HostedBy,
        MaybeCount           = MaybeCount,
        MinimumAccepted      = MinimumAccepted,
        RejectedCount        = RejectedCount,
        ScheduledFor         = ScheduledFor,
        TerrainFk            = TerrainFk,
        HostedByFk           = HostedByFk,
        ImageMimeType        = ImageMimeType,
        ModPackFk            = ModPackFk,
        ScheduledForOriginal = ScheduledForOriginal,
        IsVisible            = IsVisible,
    };

    public void Apply(Event other)
    {
        PrimaryKey           = other.PrimaryKey;
        Owner                = other.Owner;
        Title                = other.Title;
        OwnerFk              = other.OwnerFk;
        UserMetas            = other.UserMetas;
        TimeStampCreated     = other.TimeStampCreated;
        ModPack              = other.ModPack;
        Terrain              = other.Terrain;
        Description          = other.Description;
        Image                = other.Image;
        AcceptedCount        = other.AcceptedCount;
        HostedBy             = other.HostedBy;
        MaybeCount           = other.MaybeCount;
        MinimumAccepted      = other.MinimumAccepted;
        RejectedCount        = other.RejectedCount;
        ScheduledFor         = other.ScheduledFor;
        TerrainFk            = other.TerrainFk;
        HostedByFk           = other.HostedByFk;
        ImageMimeType        = other.ImageMimeType;
        ModPackFk            = other.ModPackFk;
        ScheduledForOriginal = other.ScheduledForOriginal;
        IsVisible            = other.IsVisible;
    }
}