using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace X39.UnitedTacticalForces.Api;

public static class Constants
{
    public static class Routes
    {
        public const string Events        = "events";
        public const string EventSlotting = "slotting";
        public const string Users         = "users";
        public const string ModPacks      = "mod-packs";
        public const string Terrains      = "terrains";
    }
    public static class AuthorizationSchemas
    {
        public const string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;
        public const string Steam  = SteamAuthenticationDefaults.AuthenticationScheme;
        public const string Api    = "api";
        public const string Banned = "banned";
    }

    public static class Configuration
    {
        public static class Steam
        {
            public const string ApiKey = "Steam:ApiKey";
        }

        public static class General
        {
            public const string BasePath = "General:BasePath";
        }
    }

    public static class ClaimTypes
    {
        public const string UserId = "uid";
    }

    public static class Lifetime
    {
        public const int SteamAuthDays = 7;
    }
}