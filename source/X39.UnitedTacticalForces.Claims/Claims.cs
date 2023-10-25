namespace X39.UnitedTacticalForces;

public static class Claims
{
    public static class Administrative
    {
        public const string All     = "adm";
        public const string Event   = "adm:ev";
        public const string Terrain = "adm:trn";
        public const string ModPack = "adm:mp";
        public const string User    = "adm:usr";
        public const string Server  = "adm:sv";
        public const string Wiki    = "adm:wiki";
    }

    public static class General
    {
        public const string Verified = "vrf";
    }

    public static class Creation
    {
        public const string Events  = "crt:ev";
        public const string Terrain = "crt:trn";
        public const string ModPack = "crt:mp";
        public const string Wiki    = "crt:wiki";
        public const string Server  = "crt:sv";
    }

    public static class User
    {
        public const string List          = "usr:list";
        public const string ViewSteamId64 = "usr:steamid64";
        public const string ViewMail      = "usr:mail";
        public const string Modify        = "usr:modify";
        public const string ViewDiscordId = "usr:discord";
        public const string Verify        = "usr:verify";
        public const string Ban           = "usr:ban";
    }

    public static class ResourceBased
    {
        public static class Server
        {
            public const string All           = "sv";
            public const string StartStop     = "sv:state";
            public const string Rename        = "sv:rnm";
            public const string ModPack       = "sv:mp";
            public const string Configuration = "sv:cfg";
            public const string Upgrade       = "sv:upgrd";
            public const string Files         = "sv:files";
            public const string AccessLogs    = "sv:logs";
            public const string DeleteLogs    = "sv:logs:del";
        }

        public static class Wiki
        {
            public const string All    = "wiki";
            public const string Modify = "wiki:modify";
            public const string Delete = "wiki:delete";
        }

        public static class ModPack
        {
            public const string All    = "mp";
            public const string Modify = "mp:modify";
            public const string Delete = "mp:delete";
        }

        public static class Terrain
        {
            public const string All    = "trn";
            public const string Modify = "trn:modify";
            public const string Delete = "trn:delete";
        }

        public static class Event
        {
            public const string All    = "ev";
            public const string Modify = "ev:modify";
            public const string Delete = "ev:delete";
        }
    }
}