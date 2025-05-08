using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
namespace X39.UnitedTacticalForces.WebApp.ExtensionMethods;

public static class LifetimeStatusExtensions
{
    public static Icon ToIcon(this ELifetimeStatus? self)
    {
        if (self == null)
            return new Icons.Regular.Size20.CircleHint();
        if (self.Integer == ELifetimeStatus.Stopped.Integer)
            return new Icons.Regular.Size20.RecordStop();
        if (self.Integer == ELifetimeStatus.Starting.Integer )
            return new Icons.Regular.Size20.PlayCircleHint();
        if (self.Integer == ELifetimeStatus.Stopping.Integer)
            return new Icons.Regular.Size20.PlayCircleHint();
        if (self.Integer == ELifetimeStatus.Running.Integer)
            return new Icons.Regular.Size20.PlayCircle();
        if (self.Integer == ELifetimeStatus.Updating.Integer)
            return new Icons.Regular.Size20.ArrowSyncCircle();
        throw new ArgumentOutOfRangeException(nameof(self), self, null);
    }
}
