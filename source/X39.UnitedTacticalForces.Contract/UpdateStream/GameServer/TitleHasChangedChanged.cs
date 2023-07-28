namespace X39.UnitedTacticalForces.Contract.UpdateStream.GameServer;

public class TitleHasChangedChanged
{
    public long GameServerId { get; set; }
    public string Title { get; set; } = string.Empty;
}