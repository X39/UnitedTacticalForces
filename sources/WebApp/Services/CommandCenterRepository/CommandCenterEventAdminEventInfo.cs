using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public record CommandCenterEventAdminEventInfo
{
    public PlainEventDto PlainEvent { get; init; } = new();

    public PlainUserDto? Owner { get; init; }

    public PlainUserDto? HostedBy { get; init; }

    public PlainTerrainDto? Terrain { get; init; }

    public ModPackRevisionDto? ModPackRevision { get; init; }
}
