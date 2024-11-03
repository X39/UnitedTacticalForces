using System.Text;
using X39.Util.Collections;

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

    public void Apply(Event other)
    {
        PrimaryKey           = other.PrimaryKey;
        Owner                = other.Owner;
        Title                = other.Title;
        OwnerFk              = other.OwnerFk;
        UserMetas            = other.UserMetas;
        TimeStampCreated     = other.TimeStampCreated;
        ModPackRevision      = other.ModPackRevision;
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
        ModPackRevisionFk    = other.ModPackRevisionFk;
        ScheduledForOriginal = other.ScheduledForOriginal;
        IsVisible            = other.IsVisible;
    }
    public Event DeepCopy()
    {
        return new Event
        {
            IsVisible            = IsVisible,
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
            ModPackRevision      = ModPackRevision?.PartialCopy(),
            Owner                = Owner?.PartialCopy(),
            Terrain              = Terrain?.DeepCopy(),
            Title                = Title,
            OwnerFk              = OwnerFk,
            UserMetas            = UserMetas?.NotNull().Select((q)=>q.PartialCopy()).ToList(),
            PrimaryKey           = PrimaryKey,
            ScheduledForOriginal = ScheduledForOriginal,
            TimeStampCreated     = TimeStampCreated,
            ModPackRevisionFk    = ModPackRevisionFk,
        };
    }

    public Event ShallowCopy()
    {
        return new Event
        {
            IsVisible            = IsVisible,
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
            ModPackRevision      = null,
            Owner                = null,
            Terrain              = null,
            Title                = Title,
            OwnerFk              = OwnerFk,
            UserMetas            = new List<UserEventMeta>(),
            PrimaryKey           = PrimaryKey,
            ScheduledForOriginal = ScheduledForOriginal,
            TimeStampCreated     = TimeStampCreated,
            ModPackRevisionFk    = ModPackRevisionFk,
        };
    }
}