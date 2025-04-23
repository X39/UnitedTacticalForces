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

    public static PlainUserDto ToPlainUserDto(this FullUserDto self)
        => new()
        {
            AdditionalData = self.AdditionalData,
            Avatar         = self.Avatar,
            AvatarMimeType = self.AvatarMimeType,
            Nickname       = self.Nickname,
            PrimaryKey     = self.PrimaryKey,
        };

    public static UserDto ToUserDto(this FullUserDto self)
        => new()
        {
            AdditionalData  = self.AdditionalData,
            Avatar          = self.Avatar,
            AvatarMimeType  = self.AvatarMimeType,
            DiscordId       = self.DiscordId,
            DiscordUsername = self.DiscordUsername,
            EMail           = self.EMail,
            IsBanned        = self.IsBanned,
            IsDeleted       = self.IsDeleted,
            IsVerified      = self.IsVerified,
            Nickname        = self.Nickname,
            PrimaryKey      = self.PrimaryKey,
            SteamId64       = self.SteamId64,
        };
}
