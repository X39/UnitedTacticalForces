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
}