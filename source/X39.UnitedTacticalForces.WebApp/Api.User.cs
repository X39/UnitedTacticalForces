using X39.Util.Collections;

namespace X39.UnitedTacticalForces.WebApp;

public partial class User
{
    public User PartialCopy()
    {
        return new User
        {
            PrimaryKey     = PrimaryKey,
            Avatar         = Avatar,
            Nickname       = Nickname,
            Roles          = Roles?.NotNull().Select((q) => q.ShallowCopy()).ToList(),
            EMail          = EMail,
            EventMetas     = EventMetas?.NotNull().Select((q) => q.ShallowCopy()).ToList(),
            EventSlots     = EventSlots?.NotNull().Select((q) => q.ShallowCopy()).ToList(),
            IsBanned       = IsBanned,
            IsDeleted      = IsDeleted,
            IsVerified     = IsVerified,
            Steam          = Steam?.DeepCopy() ?? new(),
            Discord        = Discord?.DeepCopy() ?? new(),
            AvatarMimeType = AvatarMimeType,
            ModPackMetas   = ModPackMetas?.NotNull().Select((q) => q.ShallowCopy()).ToList(),
        };
    }

    public User ShallowCopy()
    {
        return new User
        {
            PrimaryKey     = PrimaryKey,
            Avatar         = Avatar,
            Nickname       = Nickname,
            Roles          = new List<Role>(),
            EMail          = EMail,
            EventMetas     = new List<UserEventMeta>(),
            EventSlots     = new List<EventSlot>(),
            IsBanned       = IsBanned,
            IsDeleted      = IsDeleted,
            IsVerified     = IsVerified,
            Steam          = Steam?.DeepCopy() ?? new(),
            Discord        = Discord?.DeepCopy() ?? new(),
            AvatarMimeType = AvatarMimeType,
            ModPackMetas   = new List<UserModPackMeta>(),
        };
    }
}