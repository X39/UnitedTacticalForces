using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace X39.UnitedTacticalForces.Api;

public static class Constants
{
    public static class AuthorizationSchemas
    {
        public const string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;
        public const string Steam = SteamAuthenticationDefaults.AuthenticationScheme;
        public const string Api = "api";
    }

    public static class Configuration
    {
        public static class Steam
        {
            public const string ApiKey = "Steam:ApiKey";
        }

        public static class Jwt
        {
            public const string Issuer = nameof(Jwt) + ":" + nameof(Issuer);
            public const string Audience = nameof(Jwt) + ":" + nameof(Audience);
            public const string SecretKey = nameof(Jwt) + ":" + nameof(SecretKey);
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

    public static class Roles
    {
        public const string Admin = "admin";
        public const string EventCreate = "event-create";
        public const string EventModify = "event-modify";
        public const string EventDelete = "event-delete";
        public const string TerrainCreate = "terrain-create";
        public const string TerrainModify = "terrain-modify";
        public const string TerrainDelete = "terrain-delete";
        public const string ModPackCreate = "modpack-create";
        public const string ModPackModify = "modpack-modify";
        public const string ModPackDelete = "modpack-delete";
    }
}