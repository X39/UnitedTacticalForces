namespace X39.UnitedTacticalForces.WebApp;

public static class Constants
{
    public static class Configuration
    {
        public const string ApiBaseUrl = "Api:BaseUrl";
    }

    public static class AuthenticationTypes
    {
        public const string Steam = "Steam";
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