using Microsoft.AspNetCore.Components.Web;

namespace X39.UnitedTacticalForces.WebApp.Components;

public static class UtfCommandRowInteraction
{
    public static bool IsOpenKey(KeyboardEventArgs args)
        => args.Key is "Enter" or " " or "Spacebar";
}
