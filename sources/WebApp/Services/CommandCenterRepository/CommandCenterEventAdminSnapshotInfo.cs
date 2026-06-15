using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterEventAdminSnapshotInfo
{
    public long EventCount { get; init; }

    public CommandCenterEventAdminEventInfo[] Events { get; init; } = [];

    public UpcomingEventDto[] UpcomingEvents { get; init; } = [];
}
