using System.Diagnostics.CodeAnalysis;
using AspNet.Security.OAuth.Discord;
using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace X39.UnitedTacticalForces.Api;

internal static class Constants
{
    public static class Steam
    {
        public static class AppId
        {
            public const long Arma3Server = 233780;
            public const long Arma3       = 107410;
        }
    }

    public static class Routes
    {
        public const string Events        = "events";
        public const string GameServers   = "game-servers";
        public const string EventSlotting = "slotting";
        public const string Users         = "users";
        public const string ModPacks      = "mod-packs";
        public const string Terrains      = "terrains";
        public const string Wiki          = "wiki";
        public const string UpdateStream  = "update-stream";
    }

    public static class AuthorizationSchemas
    {
        public const string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public const string Steam = SteamAuthenticationDefaults.AuthenticationScheme;

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public const string Discord = DiscordAuthenticationDefaults.AuthenticationScheme;

        public const string Api    = "api";
        public const string Banned = "banned";
    }
    public static class Discord
    {
        public static class Commands
        {
            public const string Teamspeak   = "teamspeak";
            public const string GameServers = "gameservers";
        }
    }

    public static class Configuration
    {
        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static class Steam
        {
            public const string Enabled           = nameof(Steam) + ":" + nameof(Enabled);
            public const string ApiKey            = nameof(Steam) + ":" + nameof(ApiKey);
            public const string SteamCmdPath      = nameof(Steam) + ":" + nameof(SteamCmdPath);
            public const string InstallBasePath   = nameof(Steam) + ":" + nameof(InstallBasePath);
            public const string Username          = nameof(Steam) + ":" + nameof(Username);
            public const string Password          = nameof(Steam) + ":" + nameof(Password);
            public const string WorkshopChunkSize = nameof(Steam) + ":" + nameof(WorkshopChunkSize);
        }

        public static class Discord
        {
            public const string Enabled = nameof(Discord) + ":" + nameof(Enabled);

            public static class OAuth
            {
                public const string ClientId     = nameof(Discord) + ":" + nameof(OAuth) + ":" + nameof(ClientId);
                public const string ClientSecret = nameof(Discord) + ":" + nameof(OAuth) + ":" + nameof(ClientSecret);
            }

            public static class Bot
            {
                public const string ApplicationId = nameof(Discord) + ":" + nameof(Bot) + ":" + nameof(ApplicationId);
                public const string PublicKey     = nameof(Discord) + ":" + nameof(Bot) + ":" + nameof(PublicKey);
                public const string BotToken      = nameof(Discord) + ":" + nameof(Bot) + ":" + nameof(BotToken);
                public const string EmbedColor      = nameof(Discord) + ":" + nameof(Bot) + ":" + nameof(EmbedColor);
            }
        }

        public static class TeamSpeak
        {
            public const string Host            = nameof(TeamSpeak) + ":" + nameof(Host);
            public const string Port            = nameof(TeamSpeak) + ":" + nameof(Port);
            public const string Password        = nameof(TeamSpeak) + ":" + nameof(Password);
            public const string Channel         = nameof(TeamSpeak) + ":" + nameof(Channel);
            public const string ChannelPassword = nameof(TeamSpeak) + ":" + nameof(ChannelPassword);
        }

        public static class General
        {
            public const string ClientBaseUrl         = nameof(General) + ":" + nameof(ClientBaseUrl);
            public const string ApiBaseUrl            = nameof(General) + ":" + nameof(ApiBaseUrl);
            public const string BasePath              = nameof(General) + ":" + nameof(BasePath);
            public const string AutoVerifyNewUsers    = nameof(General) + ":" + nameof(AutoVerifyNewUsers);
            public const string GameServerHostAddress = nameof(General) + ":" + nameof(GameServerHostAddress);
        }
    }

    public static class ClaimTypes
    {
        public const string UserId = "uid";
    }

    public static class Lifetime
    {
        public const int SteamAuthDays   = 7;
        public const int DiscordAuthDays = 7;
    }
}