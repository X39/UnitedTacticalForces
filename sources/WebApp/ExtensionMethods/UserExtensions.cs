using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class UserExtensions
{

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
            DiscordId       = (string?) self.DiscordId,
            DiscordUsername = (string?) self.DiscordUsername,
            EMail           = (string?) self.EMail,
            IsBanned        = self.IsBanned,
            IsDeleted       = self.IsDeleted,
            IsVerified      = self.IsVerified,
            Nickname        = self.Nickname,
            PrimaryKey      = self.PrimaryKey,
            SteamId64       = (string?) self.SteamId64,
        };
}
