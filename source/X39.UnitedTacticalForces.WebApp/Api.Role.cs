using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class Role
{
    public Role DeepCopy()
    {
        return new Role
        {
            PrimaryKey = PrimaryKey,
            Title      = Title,
            Category   = Category,
            Identifier = Identifier,
            Users      = Users?.NotNull().Select((q) => q.PartialCopy()).ToList(),
        };
    }

    public Role ShallowCopy()
    {
        return new Role
        {
            PrimaryKey = PrimaryKey,
            Title      = Title,
            Category   = Category,
            Identifier = Identifier,
            Users      = null,
        };
    }
}