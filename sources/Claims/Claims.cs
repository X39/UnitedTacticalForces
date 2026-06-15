namespace X39.UnitedTacticalForces;

public static class Claims
{
    public static class Virtual
    {
        public const string OwnEvents = "vrt:ev";
    }
    public static class Administrative
    {
        public const string All     = "adm";
        public const string Event   = "adm:ev";
        public const string Terrain = "adm:trn";
        public const string ModPack = "adm:mp";
        public const string User    = "adm:usr";
        public const string Role    = "adm:rls";
        public const string Wiki    = "adm:wiki";
        public const string Verein  = "adm:verein";
        public const string Server  = "adm:sv";
        public const string Hosting = "adm:hosting";
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
        public const string Role  = "crt:rls";
        public const string Server  = "crt:sv";
    }

    public static class User
    {
        public const string All          = "usr";
        public const string List          = "usr:list";
        public const string ViewSteamId64 = "usr:steamid64";
        public const string Nickname      = "usr:nick";
        public const string EMail         = "usr:mail";
        public const string Modify        = "usr:modify";
        public const string ViewDiscordId = "usr:discord";
        public const string Verify        = "usr:verify";
        public const string Ban           = "usr:ban";
        public const string Roles         = "usr:rls";
    }

    public static class Wiki
    {
        public const string All    = "wiki";
        public const string Modify = "wiki:modify";
        public const string Delete = "wiki:delete";
        public const string Rename = "wiki:rename";
    }

    public static class Content
    {
        public const string Pages = "content:pages";
    }

    public static class Verein
    {
        public const string All        = "verein";
        public const string Members    = "verein:members";
        public const string Documents  = "verein:documents";
        public const string Kassenbuch = "verein:kasse";
    }

    public static class Finance
    {
        public const string All             = "fin";
        public const string ViewAll         = "fin:view-all";
        public const string RecordPayment   = "fin:record-payment";
        public const string RecordRefund    = "fin:record-refund";
        public const string CorrectLedger   = "fin:correct-ledger";
        public const string ManageAllowance = "fin:manage-allowance";
        public const string FreezeCostOwner = "fin:freeze-cost-owner";
        public const string ManagePolicy    = "fin:manage-policy";
    }

    public static class ResourceBased
    {
        public static class Server
        {
            public const string All          = "sv";
            public const string View         = "sv:view";
            public const string Operate      = "sv:operate";
            public const string Modify       = "sv:modify";
            public const string ManageAccess = "sv:manage-access";
            public const string DeleteData   = "sv:delete-data";
        }

        public static class CostOwner
        {
            public const string Use  = "co:use";
            public const string View = "co:view";
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
    }
}
