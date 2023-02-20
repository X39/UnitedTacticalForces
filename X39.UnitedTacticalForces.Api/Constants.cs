namespace X39.UnitedTacticalForces.Api;

public static class Constants
{
    public static class AuthorizationSchemas
    {
        public const string Steam = "steam";
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
}