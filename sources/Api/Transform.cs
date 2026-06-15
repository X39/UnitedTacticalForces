using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api;

internal static class Transform
{
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

    public static FullModPackDefinitionDto ToFullDto(this ModPackDefinition self)
        => new()
        {
            PrimaryKey       = self.PrimaryKey,
            TimeStampCreated = self.TimeStampCreated,
            Title            = self.Title,
            Owner            = self.Owner?.ToPlainDto(),
            IsActive         = self.IsActive,
            IsComposition    = self.IsComposition,
            ModPackRevisions = self.ModPackRevisions?.Select((modPackRevision) => modPackRevision.ToPlainDto())
                .ToArray() ?? [],
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
            SteamId64       = self.Steam.Id64.ToString(),
            DiscordId       = self.Discord.Id.ToString(),
            DiscordUsername = self.Discord.Username,
        };

    public static PlainTerrainDto ToPlainDto(this Terrain self)
        => new()
        {
            PrimaryKey    = self.PrimaryKey,
            Title         = self.Title,
            ImageAssetFk  = self.ImageAssetFk,
            ImagePath     = Services.Media.IMediaAssetService.CreateImagePath(self.ImageAssetFk),
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
