namespace X39.UnitedTacticalForces.WebApp;

public partial class LifetimeEvent
{
    public LifetimeEvent ShallowCopy()
    {
        return new LifetimeEvent
        {
            PrimaryKey   = PrimaryKey,
            GameServer   = null,
            GameServerFk = GameServerFk,
            TimeStamp    = TimeStamp,
            Status       = Status,
            ExecutedBy   = null,
            ExecutedByFk = ExecutedByFk,
        };
    }
}