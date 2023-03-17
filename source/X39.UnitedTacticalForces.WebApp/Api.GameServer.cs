using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.ExtensionMethods;

namespace X39.UnitedTacticalForces.WebApp;

public partial class GameServer
{
    private sealed class GameServerEqualityComparer : IEqualityComparer<GameServer>
    {
        public bool Equals(GameServer? x, GameServer? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Title == y.Title
                   && ((x.ActiveModPack?.PrimaryKey ?? x.ActiveModPackFk) ==
                       (y.ActiveModPack?.PrimaryKey ?? y.ActiveModPackFk))
                   && ((x.SelectedModPack?.PrimaryKey ?? x.SelectedModPackFk) ==
                       (y.SelectedModPack?.PrimaryKey ?? y.SelectedModPackFk));
        }

        public int GetHashCode(GameServer obj)
        {
            return HashCode.Combine(obj.Title, obj.ActiveModPackFk, obj.SelectedModPackFk);
        }
    }

    public static IEqualityComparer<GameServer> GameServerComparer { get; } = new GameServerEqualityComparer();

    public GameServer DeepCopy()
    {
        return new GameServer
        {
            ActiveModPack        = ActiveModPack?.Clone(),
            SelectedModPack      = SelectedModPack?.Clone(),
            ActiveModPackFk      = ActiveModPackFk,
            PrimaryKey           = PrimaryKey,
            SelectedModPackFk    = SelectedModPackFk,
            Title                = Title,
            Status               = Status,
            ConfigurationEntries = ConfigurationEntries?.Select((q) => q.ShallowCopy()).ToImmutableArray(),
            ControllerIdentifier = ControllerIdentifier,
            IsActive             = IsActive,
            LifetimeEvents       = LifetimeEvents?.Select((q) => q.ShallowCopy()).ToImmutableArray(),
            VersionString        = VersionString,
            TimeStampCreated     = TimeStampCreated,
            TimeStampUpgraded    = TimeStampUpgraded,
        };
    }
    public GameServer ShallowCopy()
    {
        return new GameServer
        {
            ActiveModPack        = null,
            SelectedModPack      = null,
            ConfigurationEntries = null,
            LifetimeEvents       = null,
            ActiveModPackFk      = ActiveModPackFk,
            PrimaryKey           = PrimaryKey,
            SelectedModPackFk    = SelectedModPackFk,
            Title                = Title,
            Status               = Status,
            ControllerIdentifier = ControllerIdentifier,
            IsActive             = IsActive,
            VersionString        = VersionString,
            TimeStampCreated     = TimeStampCreated,
            TimeStampUpgraded    = TimeStampUpgraded,
        };
    }
}