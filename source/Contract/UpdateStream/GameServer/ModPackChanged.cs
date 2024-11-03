namespace X39.UnitedTacticalForces.Contract.UpdateStream.GameServer;

public class ModPackChanged
{
    public long GameServerId { get; set; }
    public long? SelectedModPackId { get; set; }
    public long? ActiveModPackId { get; set; }
}