namespace X39.UnitedTacticalForces;

public static class Roles
{
    public const string Admin             = "admin";
    public const string EventCreate       = "event-create";
    public const string EventModify       = "event-modify";
    public const string EventDelete       = "event-delete";
    public const string TerrainCreate     = "terrain-create";
    public const string TerrainModify     = "terrain-modify";
    public const string TerrainDelete     = "terrain-delete";
    public const string ModPackCreate     = "modpack-create";
    public const string ModPackModify     = "modpack-modify";
    public const string ModPackDelete     = "modpack-delete";
    public const string UserList          = "user-list";
    public const string UserViewSteamId64 = "user-view-steamid64";
    public const string UserViewMail      = "user-view-mail";
    public const string UserModify        = "user-modify";
    
    /// <summary>
    /// Allows a user to manage the ban status (and view the ban status) of another user.
    /// </summary>
    public const string UserBan           = "user-ban";
    /// <summary>
    /// Allows a user to manage the roles of other users.
    /// Available roles to manage depend on the own role unless admin role is given.
    /// </summary>
    public const string UserManageRoles   = "user-roles-all";

    /// <summary>
    /// Allows a user to ignore slotting rules.
    /// </summary>
    public const string EventSlotIgnore = "event-slot-ignore";

    /// <summary>
    /// Allows a user to assign other users into a slot regardless of slotting rules.
    /// </summary>
    public const string EventSlotAssign = "event-slot-assign";
    /// <summary>
    /// Allows a user to create new event slots for any events.
    /// </summary>
    public const string EventSlotCreate = "event-slot-create";
    /// <summary>
    /// Allows a user to delete existing event slots for any events.
    /// </summary>
    public const string EventSlotDelete = "event-slot-delete";
}