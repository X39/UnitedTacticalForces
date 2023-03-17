using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.ExtensionMethods;

namespace X39.UnitedTacticalForces.WebApp;

public partial class ConfigurationEntry
{
    public ConfigurationEntry ShallowCopy()
    {
        return new ConfigurationEntry
        {
            PrimaryKey   = PrimaryKey,
            IsActive     = IsActive,
            GameServer   = null,
            GameServerFk = GameServerFk,
            Path         = Path,
            Realm        = Realm,
            Value        = Value,
            ChangedBy    = null,
            ChangedByFk = ChangedByFk,
            IsSensitive = IsSensitive,
            TimeStamp = TimeStamp,
        };
    }
}