using System.Text;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class UserExtensions
{
    public static string ToImageSource(this User user)
    {
        if (user.Avatar is null)
            throw new ArgumentException("User.Avatar is null");
        var builder = new StringBuilder();
        builder.Append("data:");
        builder.Append(user.AvatarMimeType);
        builder.Append("; base64, ");
        builder.Append(Convert.ToBase64String(user.Avatar));
        return builder.ToString();
    }
}