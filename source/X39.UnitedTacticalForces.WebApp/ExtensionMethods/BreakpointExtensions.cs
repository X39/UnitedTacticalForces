using MudBlazor;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class BreakpointExtensions
{
    public static string ToMudTableFullHeight(this Breakpoint self)
    {
        
        return self is Breakpoint.Sm or Breakpoint.Xs
            ? "calc(100vh - 64px - 52px - 16px - 64px - 64px - 64px)"
            : "calc(100vh - 64px - 52px)";
    }
}