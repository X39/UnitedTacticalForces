namespace X39.UnitedTacticalForces.WebApp;

public static partial class Constants
{
    public static class File
    {
        public const long Byte = 1;
        public const long Kilobyte = 1024 * Byte;
        public const long Megabyte = 1024 * Kilobyte;
        public const long Gigabyte = 1024 * Megabyte;
    }
    public const string Verified = "vrf";
    public static class Configuration
    {
        public const string ApiBaseUrl = "Api:BaseUrl";

        public static class Urls
        {
            public const string TeamSpeak3 = "Urls:TeamSpeak3";
            public const string Discord = "Urls:Discord";
        }
    }

    public static class AuthenticationTypes
    {
        public const string Steam = "Steam";
    }
}
