using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Contract.UpdateStream.GameServer;

public class LifetimeStatusHasChanged
{
    public long GameServerId { get; set; }
    public ELifetimeStatus LifetimeStatus { get; set; }
    public double Progress { get; set; }
}