namespace X39.UnitedTacticalForces.WebApp;

public partial class UserEventMeta
{
    public UserEventMeta PartialCopy()
    {
        return new UserEventMeta
        {
            User = User?.ShallowCopy(),
            Event = Event?.ShallowCopy(),
            Acceptance = Acceptance,
            EventFk = EventFk,
            UserFk = UserFk,
        };
    }

    public UserEventMeta ShallowCopy()
    {
        return new UserEventMeta
        {
            User       = null,
            Event      = null,
            Acceptance = Acceptance,
            EventFk    = EventFk,
            UserFk     = UserFk,
        };
    }
}