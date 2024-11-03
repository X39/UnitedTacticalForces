using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class DiscordUser
{
    public DiscordUser DeepCopy()
    {
        return new DiscordUser
        {
            Id       = Id,
            Username = Username,
        };
    }
}