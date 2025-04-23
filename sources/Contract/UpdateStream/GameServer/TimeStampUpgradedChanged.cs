namespace X39.UnitedTacticalForces.Contract.UpdateStream.GameServer;

public class TimeStampUpgradedChanged
{
    public long GameServerId { get; set; }
    public DateTimeOffset TimeStampUpgraded { get; set; }
}