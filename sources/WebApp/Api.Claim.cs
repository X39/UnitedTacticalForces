using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class Claim
{
    public Claim DeepCopy()
    {
        return new Claim
        {
            PrimaryKey  = PrimaryKey,
            Title       = Title,
            Users       = Users?.NotNull().Select((q) => q.PartialCopy()).ToList(),
            Description = Description,
            Roles       = Roles?.NotNull().Select((q) => q.ShallowCopy()).ToList(),
            Identifier  = Identifier,
            Category    = Category,
            Value       = Value,
            IsPrefix    = IsPrefix,
            ValueType   = ValueType,
        };
    }

    public Claim ShallowCopy()
    {
        return new Claim
        {
            PrimaryKey  = PrimaryKey,
            Title       = Title,
            Users       = new List<User>(),
            Description = Description,
            Roles       = new List<Role>(),
            Identifier  = Identifier,
            Category    = Category,
            Value       = Value,
            IsPrefix    = IsPrefix,
            ValueType   = ValueType,
        };
    }
}
