using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public sealed class Event
{
    public PlainEventDto PlainEvent { get; set; }
    public UserDto? Owner { get; set; }
    public UserDto? HostedBy { get; set; }
    public PlainModPackRevisionDto? ModPackRevision { get; set; }
    public PlainTerrainDto? Terrain { get; set; }
}
