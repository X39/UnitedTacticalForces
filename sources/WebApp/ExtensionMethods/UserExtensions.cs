using System.Text;
using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class UserExtensions
{
    public static string ToImageSource(this UserDto user)
    {
        if (user.Avatar is null || user.Avatar.Length is 0)
            throw new ArgumentException("User.Avatar is null");
        var builder = new StringBuilder();
        builder.Append("data:");
        builder.Append(user.AvatarMimeType);
        builder.Append("; base64, ");
        builder.Append(Convert.ToBase64String(user.Avatar));
        return builder.ToString();
    }
    public static string ToImageSource(this PlainUserDto user)
    {
        if (user.Avatar is null || user.Avatar.Length is 0)
            throw new ArgumentException("User.Avatar is null");
        var builder = new StringBuilder();
        builder.Append("data:");
        builder.Append(user.AvatarMimeType);
        builder.Append("; base64, ");
        builder.Append(Convert.ToBase64String(user.Avatar));
        return builder.ToString();
    }
}
