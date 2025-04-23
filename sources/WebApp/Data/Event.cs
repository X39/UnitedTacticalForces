using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class Event
{
    public required PlainEventDto PlainEvent { get; set; }
    public UserDto? Owner { get; set; }
    public UserDto? HostedBy { get; set; }
    public ModPackRevisionDto? ModPackRevision { get; set; }
    public PlainTerrainDto? Terrain { get; set; }

    public Event DeepCopy()
    {
        return new Event
        {
            PlainEvent = new PlainEventDto
            {
                AcceptedCount        = PlainEvent.AcceptedCount,
                AdditionalData       = PlainEvent.AdditionalData,
                Description          = PlainEvent.Description,
                HostedByFk           = PlainEvent.HostedByFk,
                Image                = PlainEvent.Image,
                ImageMimeType        = PlainEvent.ImageMimeType,
                IsVisible            = PlainEvent.IsVisible,
                MaybeCount           = PlainEvent.MaybeCount,
                MinimumAccepted      = PlainEvent.MinimumAccepted,
                ModPackRevisionFk    = PlainEvent.ModPackRevisionFk,
                OwnerFk              = PlainEvent.OwnerFk,
                PrimaryKey           = PlainEvent.PrimaryKey,
                RejectedCount        = PlainEvent.RejectedCount,
                ScheduledFor         = PlainEvent.ScheduledFor,
                ScheduledForOriginal = PlainEvent.ScheduledForOriginal,
                TerrainFk            = PlainEvent.TerrainFk,
                TimeStampCreated     = PlainEvent.TimeStampCreated,
                Title                = PlainEvent.Title, MetaAcceptance = PlainEvent.MetaAcceptance,
            },
            Owner = Owner is null
                ? null
                : new UserDto
                {
                    AdditionalData  = Owner.AdditionalData,
                    Avatar          = Owner.Avatar,
                    AvatarMimeType  = Owner.AvatarMimeType,
                    DiscordId       = Owner.DiscordId,
                    DiscordUsername = Owner.DiscordUsername,
                    EMail           = Owner.EMail,
                    IsBanned        = Owner.IsBanned,
                    IsDeleted       = Owner.IsDeleted,
                    IsVerified      = Owner.IsVerified,
                    Nickname        = Owner.Nickname,
                    PrimaryKey      = Owner.PrimaryKey,
                    SteamId64       = Owner.SteamId64,
                },
            HostedBy = HostedBy is null
                ? null
                : new UserDto
                {
                    AdditionalData  = HostedBy.AdditionalData,
                    Avatar          = HostedBy.Avatar,
                    AvatarMimeType  = HostedBy.AvatarMimeType,
                    DiscordId       = HostedBy.DiscordId,
                    DiscordUsername = HostedBy.DiscordUsername,
                    EMail           = HostedBy.EMail,
                    IsBanned        = HostedBy.IsBanned,
                    IsDeleted       = HostedBy.IsDeleted,
                    IsVerified      = HostedBy.IsVerified,
                    Nickname        = HostedBy.Nickname,
                    PrimaryKey      = HostedBy.PrimaryKey,
                    SteamId64       = HostedBy.SteamId64,
                },
            ModPackRevision = ModPackRevision is null
                ? null
                : new ModPackRevisionDto
                {
                    AdditionalData             = ModPackRevision.AdditionalData,
                    DefinitionIsActive         = ModPackRevision.DefinitionIsActive,
                    DefinitionIsComposition    = ModPackRevision.DefinitionIsComposition,
                    DefinitionOwnerFk          = ModPackRevision.DefinitionOwnerFk,
                    DefinitionPrimaryKey       = ModPackRevision.DefinitionPrimaryKey,
                    DefinitionTimeStampCreated = ModPackRevision.DefinitionTimeStampCreated,
                    DefinitionTitle            = ModPackRevision.DefinitionTitle,
                    Html                       = ModPackRevision.Html,
                    IsActive                   = ModPackRevision.IsActive,
                    MetaTimeStampDownloaded    = ModPackRevision.MetaTimeStampDownloaded,
                    PrimaryKey                 = ModPackRevision.PrimaryKey,
                    TimeStampCreated           = ModPackRevision.TimeStampCreated,
                    UpdatedByFk                = ModPackRevision.UpdatedByFk,
                },
            Terrain = Terrain is null
                ? null
                : new PlainTerrainDto
                {
                    AdditionalData = Terrain.AdditionalData,
                    Image          = Terrain.Image,
                    ImageMimeType  = Terrain.ImageMimeType,
                    IsActive       = Terrain.IsActive,
                    PrimaryKey     = Terrain.PrimaryKey,
                    Title          = Terrain.Title,
                },
        };
    }

    public void Apply(Event updated)
    {
        PlainEvent = new PlainEventDto
        {
            AcceptedCount        = updated.PlainEvent.AcceptedCount,
            AdditionalData       = updated.PlainEvent.AdditionalData,
            Description          = updated.PlainEvent.Description,
            HostedByFk           = updated.PlainEvent.HostedByFk,
            Image                = updated.PlainEvent.Image,
            ImageMimeType        = updated.PlainEvent.ImageMimeType,
            IsVisible            = updated.PlainEvent.IsVisible,
            MaybeCount           = updated.PlainEvent.MaybeCount,
            MinimumAccepted      = updated.PlainEvent.MinimumAccepted,
            ModPackRevisionFk    = updated.PlainEvent.ModPackRevisionFk,
            OwnerFk              = updated.PlainEvent.OwnerFk,
            PrimaryKey           = updated.PlainEvent.PrimaryKey,
            RejectedCount        = updated.PlainEvent.RejectedCount,
            ScheduledFor         = updated.PlainEvent.ScheduledFor,
            ScheduledForOriginal = updated.PlainEvent.ScheduledForOriginal,
            TerrainFk            = updated.PlainEvent.TerrainFk,
            TimeStampCreated     = updated.PlainEvent.TimeStampCreated,
            Title                = updated.PlainEvent.Title,
            MetaAcceptance       = updated.PlainEvent.MetaAcceptance,
        };
        Owner = updated.Owner is null
            ? null
            : new UserDto
            {
                AdditionalData  = updated.Owner.AdditionalData,
                Avatar          = updated.Owner.Avatar,
                AvatarMimeType  = updated.Owner.AvatarMimeType,
                DiscordId       = updated.Owner.DiscordId,
                DiscordUsername = updated.Owner.DiscordUsername,
                EMail           = updated.Owner.EMail,
                IsBanned        = updated.Owner.IsBanned,
                IsDeleted       = updated.Owner.IsDeleted,
                IsVerified      = updated.Owner.IsVerified,
                Nickname        = updated.Owner.Nickname,
                PrimaryKey      = updated.Owner.PrimaryKey,
                SteamId64       = updated.Owner.SteamId64,
            };
        HostedBy = updated.HostedBy is null
            ? null
            : new UserDto
            {
                AdditionalData  = updated.HostedBy.AdditionalData,
                Avatar          = updated.HostedBy.Avatar,
                AvatarMimeType  = updated.HostedBy.AvatarMimeType,
                DiscordId       = updated.HostedBy.DiscordId,
                DiscordUsername = updated.HostedBy.DiscordUsername,
                EMail           = updated.HostedBy.EMail,
                IsBanned        = updated.HostedBy.IsBanned,
                IsDeleted       = updated.HostedBy.IsDeleted,
                IsVerified      = updated.HostedBy.IsVerified,
                Nickname        = updated.HostedBy.Nickname,
                PrimaryKey      = updated.HostedBy.PrimaryKey,
                SteamId64       = updated.HostedBy.SteamId64,
            };
        ModPackRevision = updated.ModPackRevision is null
            ? null
            : new ModPackRevisionDto
            {
                AdditionalData             = updated.ModPackRevision.AdditionalData,
                DefinitionIsActive         = updated.ModPackRevision.DefinitionIsActive,
                DefinitionIsComposition    = updated.ModPackRevision.DefinitionIsComposition,
                DefinitionOwnerFk          = updated.ModPackRevision.DefinitionOwnerFk,
                DefinitionPrimaryKey       = updated.ModPackRevision.DefinitionPrimaryKey,
                DefinitionTimeStampCreated = updated.ModPackRevision.DefinitionTimeStampCreated,
                DefinitionTitle            = updated.ModPackRevision.DefinitionTitle,
                Html                       = updated.ModPackRevision.Html,
                IsActive                   = updated.ModPackRevision.IsActive,
                MetaTimeStampDownloaded    = updated.ModPackRevision.MetaTimeStampDownloaded,
                PrimaryKey                 = updated.ModPackRevision.PrimaryKey,
                TimeStampCreated           = updated.ModPackRevision.TimeStampCreated,
                UpdatedByFk                = updated.ModPackRevision.UpdatedByFk,
            };
        Terrain = updated.Terrain is null
            ? null
            : new PlainTerrainDto
            {
                AdditionalData = updated.Terrain.AdditionalData,
                Image          = updated.Terrain.Image,
                ImageMimeType  = updated.Terrain.ImageMimeType,
                IsActive       = updated.Terrain.IsActive,
                PrimaryKey     = updated.Terrain.PrimaryKey,
                Title          = updated.Terrain.Title,
            };
    }
}
