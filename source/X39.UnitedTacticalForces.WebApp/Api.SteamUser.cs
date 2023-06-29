using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class SteamUser
{
    public SteamUser DeepCopy()
    {
        return new SteamUser
        {
            Id64 = Id64,
        };
    }
}