using MudBlazor;

namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class LifetimeStatusExtensions
{
    public static string ToIcon(this ELifetimeStatus? self) => self switch
    {
        ELifetimeStatus.Stopped  => Icons.Material.Filled.StopCircle,
        ELifetimeStatus.Starting => Icons.Material.Filled.ChangeCircle,
        ELifetimeStatus.Stopping => Icons.Material.Filled.ChangeCircle,
        ELifetimeStatus.Running  => Icons.Material.Filled.PlayCircle,
        ELifetimeStatus.Updating => Icons.Material.Filled.ChangeCircle,
        null                     => Icons.Material.Filled.Circle,
        _                        => throw new ArgumentOutOfRangeException(nameof(self), self, null),
    };
}