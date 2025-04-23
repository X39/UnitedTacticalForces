using X39.UnitedTacticalForces.Contract.Event;

namespace X39.UnitedTacticalForces.Contract.UpdateStream.Eventing;

public class AcceptanceChanged
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public EEventAcceptance EventAcceptance { get; set; }
    public short? SlotNumber { get; set; }
}