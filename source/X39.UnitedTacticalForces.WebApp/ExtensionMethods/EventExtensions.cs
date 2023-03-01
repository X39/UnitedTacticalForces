using System.Text;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class EventExtensions
{
    public static string ToImageSource(this Event @event)
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
    public static Event Clone(this Event self) => new()
    {
        PrimaryKey = self.PrimaryKey,
        Owner = self.Owner,
        Title = self.Title,
        OwnerFk = self.OwnerFk,
        UserMetas = self.UserMetas,
        TimeStampCreated = self.TimeStampCreated,
        ModPack = self.ModPack,
        Terrain = self.Terrain,
        Description = self.Description,
        Image = self.Image,
        AcceptedCount = self.AcceptedCount,
        HostedBy = self.HostedBy,
        MaybeCount = self.MaybeCount,
        MinimumAccepted = self.MinimumAccepted,
        RejectedCount = self.RejectedCount,
        ScheduledFor = self.ScheduledFor,
        TerrainFk = self.TerrainFk,
        HostedByFk = self.HostedByFk,
        ImageMimeType = self.ImageMimeType,
        ModPackFk = self.ModPackFk,
        ScheduledForOriginal = self.ScheduledForOriginal,
    };
    public static void Apply(this Event self, Event other)
    {
        self.PrimaryKey = other.PrimaryKey;
        self.Owner = other.Owner;
        self.Title = other.Title;
        self.OwnerFk = other.OwnerFk;
        self.UserMetas = other.UserMetas;
        self.TimeStampCreated = other.TimeStampCreated;
        self.ModPack = other.ModPack;
        self.Terrain = other.Terrain;
        self.Description = other.Description;
        self.Image = other.Image;
        self.AcceptedCount = other.AcceptedCount;
        self.HostedBy = other.HostedBy;
        self.MaybeCount = other.MaybeCount;
        self.MinimumAccepted = other.MinimumAccepted;
        self.RejectedCount = other.RejectedCount;
        self.ScheduledFor = other.ScheduledFor;
        self.TerrainFk = other.TerrainFk;
        self.HostedByFk = other.HostedByFk;
        self.ImageMimeType = other.ImageMimeType;
        self.ModPackFk = other.ModPackFk;
        self.ScheduledForOriginal = other.ScheduledForOriginal;
    }
}