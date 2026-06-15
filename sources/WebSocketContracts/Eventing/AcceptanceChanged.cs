using X39.UnitedTacticalForces.Common.Event;

namespace X39.UnitedTacticalForces.WebSockets.Contracts.Eventing;

public class AcceptanceChanged
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public EEventAcceptance EventAcceptance { get; set; }
    public short? SlotNumber { get; set; }
}