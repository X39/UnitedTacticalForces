using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class Role
{
    public Role DeepCopy()
    {
        return new Role
        {
            PrimaryKey  = PrimaryKey,
            Title       = Title,
            Users       = Users?.NotNull().Select((q) => q.PartialCopy()).ToList(),
            Claims      = Claims?.NotNull().Select((q) => q.ShallowCopy()).ToList(),
            Description = Description,
        };
    }

    public Role ShallowCopy()
    {
        return new Role
        {
            PrimaryKey = PrimaryKey,
            Title      = Title,
            Users      = null,
            Claims     = new List<Claim>(),
            Description = Description,
        };
    }
}
