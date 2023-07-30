using MudBlazor;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class BreakpointExtensions
{
    public static string ToFullPageTableHeight(this Breakpoint self)
    {
        return self is Breakpoint.Sm or Breakpoint.Xs
            ? "calc(100dvh - 64px - 52px - 16px - 64px - 64px - 64px)"
            : "calc(100dvh - 64px - 52px)";
    }
    public static string ToFullPageHeightWithBigQuestionmarks(this Breakpoint self)
    {
        return self is Breakpoint.Sm or Breakpoint.Xs
            ? "calc(100dvh - 16px - 64px - 64px - 64px)"
            : "100dvh";
    }
    public static string ToFullPageHeight(this Breakpoint self)
    {
        return self is Breakpoint.Sm or Breakpoint.Xs
            ? "calc(100dvh - 64px)"
            : "100dvh";
    }
}