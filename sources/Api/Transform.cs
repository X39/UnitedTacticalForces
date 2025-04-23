using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api;

internal static class Transform
{
    public static PlainGameServerDto ToPlainDto(this GameServer self)
        => new PlainGameServerDto
        {
            PrimaryKey           = self.PrimaryKey,
            Title                = self.Title,
            TimeStampCreated     = self.TimeStampCreated,
            TimeStampUpgraded    = self.TimeStampUpgraded,
            ActiveModPackFk      = self.ActiveModPackFk,
            SelectedModPackFk    = self.SelectedModPackFk,
            Status               = self.Status,
            VersionString        = self.VersionString,
            ControllerIdentifier = self.ControllerIdentifier,
            IsActive             = self.IsActive,
        };

    public static PlainModPackDefinitionDto ToPlainDto(this ModPackDefinition self)
        => new()
        {
            PrimaryKey       = self.PrimaryKey,
            TimeStampCreated = self.TimeStampCreated,
            Title            = self.Title,
            OwnerFk          = self.OwnerFk,
            IsActive         = self.IsActive,
            IsComposition    = self.IsComposition,
        };

    public static PlainModPackRevisionDto ToPlainDto(this ModPackRevision self)
        => new()
        {
            PrimaryKey       = self.PrimaryKey,
            TimeStampCreated = self.TimeStampCreated,
            Html             = self.Html,
            UpdatedByFk      = self.UpdatedByFk,
            IsActive         = self.IsActive,
            DefinitionFk     = self.DefinitionFk,
        };

    public static PlainUserDto ToPlainDto(this User self)
        => new()
        {
            PrimaryKey     = self.PrimaryKey,
            Nickname       = self.Nickname,
            Avatar         = self.Avatar,
            AvatarMimeType = self.AvatarMimeType,
        };

    public static UserDto ToDto(this User self)
        => new()
        {
            PrimaryKey      = self.PrimaryKey,
            Nickname        = self.Nickname,
            EMail           = self.EMail,
            IsBanned        = self.IsBanned,
            Avatar          = self.Avatar,
            AvatarMimeType  = self.AvatarMimeType,
            IsVerified      = self.IsVerified,
            IsDeleted       = self.IsDeleted,
            SteamId64       = self.Steam.Id64,
            DiscordId       = self.Discord.Id,
            DiscordUsername = self.Discord.Username,
        };

    public static PlainTerrainDto ToPlainDto(this Terrain self)
        => new()
        {
            PrimaryKey    = self.PrimaryKey,
            Title         = self.Title,
            Image         = self.Image,
            ImageMimeType = self.ImageMimeType,
            IsActive      = self.IsActive,
        };

    public static PlainClaimDto ToPlainDto(this Claim self)
        => new()
        {
            PrimaryKey  = self.PrimaryKey,
            Category    = self.Category,
            Title       = self.Title,
            Identifier  = self.Identifier,
            Description = self.Description,
            IsPrefix    = self.IsPrefix,
            Value       = self.Value,
            ValueType   = self.ValueType,
        };

    public static PlainRoleDto ToPlainDto(this Role self)
        => new()
        {
            PrimaryKey  = self.PrimaryKey,
            Title       = self.Title,
            Description = self.Description,
        };
}
